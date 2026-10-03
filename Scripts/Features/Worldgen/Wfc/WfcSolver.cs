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
    private readonly WfcCollapseEffects _collapseEffects;
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
        _collapseEffects = new WfcCollapseEffects(blobTracker, spatialCoherence, tileRegistry);
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
        _collapseEffects = new WfcCollapseEffects(blobTracker, spatialCoherence, tileRegistry);
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
        var run = new WfcSolveRun(topology, biome, rng, topology.CellCount);
        PrepareForSolve(topology);

        // Initial propagation to apply any pre-existing constraints
        var initialResult = _propagator.PropagateAll(topology);
        if (!initialResult.Success)
        {
            return WfcSolveResult.Failed(
                "Initial propagation found contradiction",
                0,
                initialResult.ContradictionCellId);
        }

        var iterations = 0;
        while (!topology.IsFullyCollapsed())
        {
            iterations++;

            if (iterations > MaxIterations)
                return FailIterationBudget(iterations, run.TotalCells);

            LogProgress(iterations, run.TotalCells);

            var outcome = CollapseNextCell(run, iterations);
            if (outcome.Status == WfcStepStatus.Complete)
                break;

            if (outcome.Status == WfcStepStatus.Failed)
                return outcome.Failure;
        }

        GD.Print($"[WFC] Success! Completed {iterations} iterations for {run.TotalCells} cells");
        return WfcSolveResult.Succeeded(iterations);
    }

    /// <summary>
    /// Clears per-solve state and registers the constraints that invalidate cells beyond neighbors.
    /// </summary>
    private void PrepareForSolve(IWfcTopology topology)
    {
        _collapseEffects.Reset(topology);
        _entropyCache.Reset();

        _entropyCache.ClearInvalidators();
        foreach (var constraint in _selector.GetConstraints())
        {
            if (constraint is IEntropyInvalidator invalidator)
                _entropyCache.RegisterInvalidator(invalidator);
        }
    }

    private WfcSolveResult FailIterationBudget(int iterations, int totalCells)
    {
        GD.Print($"[WFC] Exceeded MaxIterations at {iterations}, collapsed ~{iterations}/{totalCells}");
        return WfcSolveResult.Failed(
            $"Exceeded maximum iterations ({MaxIterations})",
            iterations);
    }

    private static void LogProgress(int iterations, int totalCells)
    {
        // Progress logging every 100 iterations (iterations ~ collapsed cells)
        if (iterations % 100 == 0)
            GD.Print($"[WFC] Progress: {iterations}/{totalCells} cells ({100 * iterations / totalCells}%)");
    }

    /// <summary>
    /// Picks the lowest-entropy cell, collapses it, and propagates the result.
    /// </summary>
    private WfcStepOutcome CollapseNextCell(WfcSolveRun run, int iterations)
    {
        var topology = run.Topology;
        var targetCellId = FindLowestEntropyCell(run);

        // All cells collapsed - we are done
        if (targetCellId == null)
            return WfcStepOutcome.Complete;

        var targetCell = topology.GetCell(targetCellId.Value);

        // Check for contradiction before collapse
        if (targetCell.IsContradiction())
        {
            GD.Print($"[WFC] Contradiction at cell {targetCellId} after {iterations}/{run.TotalCells} cells");
            return WfcStepOutcome.Failed(WfcSolveResult.Failed(
                "Found cell with no valid options",
                iterations,
                targetCellId));
        }

        var selectedTile = SelectTileFor(run, targetCellId.Value);
        if (selectedTile == null)
        {
            GD.Print(
                $"[WFC] Selector returned null at cell {targetCellId} after " +
                $"{iterations}/{run.TotalCells} cells, validTiles={targetCell.GetPossibleTiles().Count}");
            return WfcStepOutcome.Failed(WfcSolveResult.Failed(
                "Tile selector returned null",
                iterations,
                targetCellId));
        }

        CollapseCell(topology, targetCellId.Value, selectedTile);

        // Propagate constraints to neighbors
        PropagationResult propResult;
        using (_profiler.BeginScope("Propagation"))
        {
            propResult = _propagator.Propagate(topology, targetCellId.Value);
        }

        if (propResult.Success)
            return WfcStepOutcome.Collapsed;

        GD.Print(
            $"[WFC] Propagation failed at cell {propResult.ContradictionCellId} after placing " +
            $"{selectedTile} at cell {targetCellId}, {iterations}/{run.TotalCells} cells");
        return WfcStepOutcome.Failed(WfcSolveResult.Failed(
            $"Propagation failed at cell {propResult.ContradictionCellId}",
            iterations,
            propResult.ContradictionCellId));
    }

    /// <summary>
    /// Finds the cell with lowest weighted entropy using the incremental cache.
    /// </summary>
    private int? FindLowestEntropyCell(WfcSolveRun run)
    {
        var topology = run.Topology;

        return _entropyCache.GetLowestEntropyCellId(
            topology,
            cellId =>
            {
                var weights = _selector.ComputeWeights(
                    topology.GetCell(cellId).GetPossibleTiles(),
                    run.Biome,
                    run.Rng,
                    GetContinuityMatchingTiles(topology, cellId),
                    cellId,
                    topology);
                return topology.GetCell(cellId).GetWeightedEntropy(weights);
            },
            run.Rng);
    }

    /// <summary>
    /// Selects a tile by weighted probability, biased toward tiles matching collapsed neighbors.
    /// </summary>
    private string? SelectTileFor(WfcSolveRun run, int cellId)
    {
        var continuityTiles = GetContinuityMatchingTiles(run.Topology, cellId);

        using (_profiler.BeginScope("TileSelection"))
        {
            return _selector.SelectTile(
                run.Topology.GetCell(cellId).GetPossibleTiles(),
                run.Biome,
                run.Rng,
                continuityTiles,
                cellId,
                run.Topology);
        }
    }

    private void CollapseCell(IWfcTopology topology, int cellId, string tileId)
    {
        using (_profiler.BeginScope("CellCollapse"))
        {
            topology.GetCell(cellId).CollapseTo(tileId);
            _collapseEffects.Apply(topology, cellId, tileId);

            // Mark affected cells dirty for entropy recalculation
            _entropyCache.OnCellCollapsed(cellId, tileId, topology);
        }
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
}
