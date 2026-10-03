using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Main WFC algorithm solver. Orchestrates cell selection, collapse, and propagation.
/// Topology-agnostic: works with rectangular grids, irregular meshes, or any IWfcTopology.
/// </summary>
public class WfcSolver
{
    private readonly WfcPropagator _propagator;
    private readonly WfcTileSelector _selector;
    private readonly ITileRegistry? _tileRegistry;
    private readonly BlobSizeTracker? _blobTracker;
    private readonly SpatialCoherenceConstraint? _spatialCoherence;
    private readonly EntropyCache _entropyCache = new();
    private IProfiler _profiler = new NoOpProfiler();

    /// <summary>
    /// Maximum iterations before giving up (prevents infinite loops).
    /// Default is 10000 which should handle up to ~100x100 grids.
    /// </summary>
    public int MaxIterations { get; set; } = 10000;

    public WfcSolver(
        WfcPropagator propagator,
        WfcTileSelector selector,
        BlobSizeTracker? blobTracker = null,
        SpatialCoherenceConstraint? spatialCoherence = null,
        ITileRegistry? tileRegistry = null)
    {
        _propagator = propagator;
        _selector = selector;
        _tileRegistry = tileRegistry;
        _blobTracker = blobTracker;
        _spatialCoherence = spatialCoherence;
    }

    /// <summary>
    /// Creates a WFC solver with connectivity tracking (for backward compatibility).
    /// Note: PassabilityGraph is not used in topology-agnostic solving,
    /// but maintained for API compatibility.
    /// </summary>
    public WfcSolver(
        WfcPropagator propagator,
        WfcTileSelector selector,
        BlobSizeTracker? blobTracker,
        Connectivity.PassabilityGraph passabilityGraph,
        Func<string, bool> isPassable,
        SpatialCoherenceConstraint? spatialCoherence = null,
        ITileRegistry? tileRegistry = null)
    {
        _propagator = propagator;
        _selector = selector;
        _tileRegistry = tileRegistry;
        _blobTracker = blobTracker;
        _spatialCoherence = spatialCoherence;
        // Note: passabilityGraph and isPassable not stored as they're grid-specific
        // Connectivity constraint added to selector handles this
    }

    public void SetProfiler(IProfiler profiler)
    {
        _profiler = profiler;
    }

    /// <summary>
    /// Runs the WFC algorithm until the topology is fully collapsed or a contradiction occurs.
    /// </summary>
    /// <param name="topology">The WFC topology to solve</param>
    /// <param name="biome">Biome for soft rule weighting</param>
    /// <param name="rng">Random number generator for tile selection</param>
    /// <returns>Result indicating success or failure with details</returns>
    public WfcSolveResult Solve(IWfcTopology topology, BiomeDefinition? biome, RandomNumberGenerator rng)
    {
        var iterations = 0;
        var totalCells = topology.CellCount;

        // Clear state for fresh solve
        _blobTracker?.Clear();
        _entropyCache.Reset();

        // Reset spatial coherence region tracking for grid topologies
        if (_spatialCoherence != null && topology is WfcGrid gridForReset)
        {
            _spatialCoherence.Reset(gridForReset.Width, gridForReset.Height);
        }

        // Register constraints that need to invalidate cells beyond neighbors
        _entropyCache.ClearInvalidators();
        foreach (var constraint in _selector.GetConstraints())
        {
            if (constraint is IEntropyInvalidator invalidator)
                _entropyCache.RegisterInvalidator(invalidator);
        }

        // Initial propagation to apply any pre-existing constraints
        var initialResult = _propagator.PropagateAll(topology);
        if (!initialResult.Success)
        {
            return WfcSolveResult.Failed(
                "Initial propagation found contradiction",
                0,
                initialResult.ContradictionCellId);
        }

        while (!topology.IsFullyCollapsed())
        {
            iterations++;

            if (iterations > MaxIterations)
            {
                GD.Print($"[WFC] Exceeded MaxIterations at {iterations}, collapsed ~{iterations}/{totalCells}");
                return WfcSolveResult.Failed(
                    $"Exceeded maximum iterations ({MaxIterations})",
                    iterations);
            }

            // Progress logging every 100 iterations (iterations ≈ collapsed cells)
            if (iterations % 100 == 0)
            {
                GD.Print($"[WFC] Progress: {iterations}/{totalCells} cells ({100*iterations/totalCells}%)");
            }

            // Find cell with lowest weighted entropy using incremental cache
            var targetCellId = _entropyCache.GetLowestEntropyCellId(
                topology,
                cellId =>
                {
                    var weights = _selector.ComputeWeights(
                        topology.GetCell(cellId).GetPossibleTiles(),
                        biome,
                        rng,
                        GetContinuityMatchingTiles(topology, cellId),
                        cellId,
                        topology);
                    return topology.GetCell(cellId).GetWeightedEntropy(weights);
                },
                rng);

            if (targetCellId == null)
            {
                // All cells collapsed - we're done
                break;
            }

            var targetCell = topology.GetCell(targetCellId.Value);

            // Check for contradiction before collapse
            if (targetCell.IsContradiction())
            {
                GD.Print($"[WFC] Contradiction at cell {targetCellId} after {iterations}/{totalCells} cells");
                return WfcSolveResult.Failed(
                    "Found cell with no valid options",
                    iterations,
                    targetCellId);
            }

            // Get tiles matching collapsed neighbors for continuity bias
            var continuityTiles = GetContinuityMatchingTiles(topology, targetCellId.Value);

            // Select tile using weighted probabilities
            string? selectedTile;
            using (_profiler.BeginScope("TileSelection"))
            {
                selectedTile = _selector.SelectTile(
                    targetCell.GetPossibleTiles(),
                    biome,
                    rng,
                    continuityTiles,
                    targetCellId.Value,
                    topology);
            }

            if (selectedTile == null)
            {
                GD.Print(
                    $"[WFC] Selector returned null at cell {targetCellId} after " +
                    $"{iterations}/{totalCells} cells, validTiles={targetCell.GetPossibleTiles().Count}");
                return WfcSolveResult.Failed(
                    "Tile selector returned null",
                    iterations,
                    targetCellId);
            }

            // Collapse the cell
            using (_profiler.BeginScope("CellCollapse"))
            {
                targetCell.CollapseTo(selectedTile);

                // Update blob tracker for soft modifiers
                if (_blobTracker != null)
                {
                    if (topology is WfcGrid grid)
                    {
                        _blobTracker.RegisterCollapse(grid.CellIdToPosition(targetCellId.Value), selectedTile, grid);
                    }
                    else
                    {
                        _blobTracker.RegisterCollapse(targetCellId.Value, selectedTile, topology);
                    }
                }

                // Update spatial coherence for region tracking (grid-specific for now)
                if (_spatialCoherence != null && topology is WfcGrid gridForCoherence)
                {
                    _spatialCoherence.OnTileCollapsed(
                        gridForCoherence.CellIdToPosition(targetCellId.Value),
                        selectedTile,
                        gridForCoherence);
                }

                // Reserve cells for multi-cell variants (grid-specific)
                if (topology is WfcGrid gridForReserve)
                {
                    ReserveMultiCellVariant(
                        gridForReserve.CellIdToPosition(targetCellId.Value),
                        selectedTile,
                        gridForReserve);
                }

                // Mark affected cells dirty for entropy recalculation
                _entropyCache.OnCellCollapsed(targetCellId.Value, selectedTile, topology);
            }

            // Propagate constraints to neighbors
            PropagationResult propResult;
            using (_profiler.BeginScope("Propagation"))
            {
                propResult = _propagator.Propagate(topology, targetCellId.Value);
            }
            if (!propResult.Success)
            {
                GD.Print(
                    $"[WFC] Propagation failed at cell {propResult.ContradictionCellId} after placing " +
                    $"{selectedTile} at cell {targetCellId}, {iterations}/{totalCells} cells");
                return WfcSolveResult.Failed(
                    $"Propagation failed at cell {propResult.ContradictionCellId}",
                    iterations,
                    propResult.ContradictionCellId);
            }
        }

        GD.Print($"[WFC] Success! Completed {iterations} iterations for {totalCells} cells");
        return WfcSolveResult.Succeeded(iterations);
    }

    /// <summary>
    /// Attempts to solve with automatic retry on contradiction.
    /// Each retry uses a fresh topology with a different random seed.
    /// </summary>
    /// <param name="createTopology">Factory function to create a fresh topology</param>
    /// <param name="biome">Biome for soft rule weighting</param>
    /// <param name="baseSeed">Base seed for random number generation</param>
    /// <param name="maxRetries">Maximum retry attempts (default 3)</param>
    /// <returns>Named result containing the solve result and last attempt's topology</returns>
    internal WfcSolveAttempt SolveWithRetry(
        Func<IWfcTopology> createTopology,
        BiomeDefinition? biome,
        ulong baseSeed,
        int maxRetries = 3)
    {
        IWfcTopology? lastTopology = null;
        WfcSolveResult lastResult = default;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var topology = createTopology();
            lastTopology = topology;

            var rng = new RandomNumberGenerator();
            rng.Seed = baseSeed + (ulong)attempt;

            lastResult = Solve(topology, biome, rng);

            if (lastResult.Success)
            {
                return new WfcSolveAttempt(lastResult, topology);
            }
        }

        return new WfcSolveAttempt(
            WfcSolveResult.Failed(
                $"All {maxRetries + 1} attempts failed. Last error: {lastResult.ErrorMessage}",
                lastResult.Iterations,
                lastResult.ContradictionCellId),
            lastTopology!);
    }

    /// <summary>
    /// Gets the set of tile IDs from collapsed neighbors at the given cell.
    /// Used to apply continuity bias in tile selection.
    /// </summary>
    private static HashSet<string>? GetContinuityMatchingTiles(IWfcTopology topology, int cellId)
    {
        HashSet<string>? result = null;

        foreach (var neighborId in topology.GetNeighbors(cellId))
        {
            var neighborCell = topology.GetCell(neighborId);
            if (neighborCell.IsCollapsed())
            {
                result ??= new HashSet<string>();
                result.Add(neighborCell.GetCollapsedTile());
            }
        }

        return result;
    }

    /// <summary>
    /// Reserves cells occupied by multi-cell variants after a cell collapse.
    /// This is grid-specific (requires positional offsets).
    /// </summary>
    private void ReserveMultiCellVariant(Vector2I anchorPos, string tileId, WfcGrid grid)
    {
        if (_tileRegistry == null)
            return;

        var tileDef = _tileRegistry.GetTile(tileId);
        if (tileDef == null || !tileDef.HasAutoTileVariants)
            return;

        var format = tileDef.GetAutoTileFormat();
        if (format == null)
            return;

        var bounds = format.GetMaxMultiCellBounds();
        if (bounds == null)
            return;

        var (size, offset) = bounds.Value;

        for (var dy = 0; dy < size.Y; dy++)
        {
            for (var dx = 0; dx < size.X; dx++)
            {
                if (dx == 0 && dy == 0 && offset == Vector2I.Zero)
                    continue;

                var reservedPos = anchorPos + offset + new Vector2I(dx, dy);

                if (!grid.IsInBounds(reservedPos))
                    continue;

                if (reservedPos == anchorPos)
                    continue;

                var cell = grid.GetCell(reservedPos);

                if (!cell.IsCollapsed() && !cell.IsReserved)
                {
                    cell.Reserve(anchorPos);
                }
            }
        }
    }
}
