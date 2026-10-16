using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;

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
    private long _propagationTicks;
    private long _tileSelectionTicks;
    private long _cellCollapseTicks;

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
        IWfcTileCatalog? tileCatalog = null)
    {
        _propagator = propagator;
        _selector = selector;
        _collapseEffects = new WfcCollapseEffects(blobTracker, spatialCoherence, tileCatalog);
    }

    /// <summary>
    /// Creates a WFC solver with connectivity tracking. Each collapsed passable grid cell is
    /// added to <paramref name="passabilityGraph"/> so connectivity constraints see the real map.
    /// </summary>
    public WfcSolver(
        WfcPropagator propagator,
        WfcTileSelector selector,
        BlobSizeTracker? blobTracker,
        Connectivity.PassabilityGraph passabilityGraph,
        Func<string, bool> isPassable,
        SpatialCoherenceConstraint? spatialCoherence = null,
        IWfcTileCatalog? tileCatalog = null)
    {
        _propagator = propagator;
        _selector = selector;
        _collapseEffects = new WfcCollapseEffects(
            blobTracker,
            spatialCoherence,
            tileCatalog,
            new Connectivity.PassabilityGraphUpdater(passabilityGraph, isPassable));
    }

    /// <summary>
    /// Runs the WFC algorithm until the topology is fully collapsed or a contradiction occurs.
    /// </summary>
    /// <param name="topology">The WFC topology to solve</param>
    /// <param name="biome">Biome for soft rule weighting</param>
    /// <param name="rng">Random number generator for tile selection</param>
    /// <param name="cancellationToken">
    /// Checked at the start of the call and once per collapsed cell.
    /// </param>
    /// <returns>Result indicating success or failure with details</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the token is cancelled. A token cancelled before the call
    /// throws before any solver state is reset or any cell changes.
    /// </exception>
    public WfcSolveResult Solve(
        IWfcTopology topology,
        BiomeDefinition? biome,
        IRandom rng,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var run = new WfcSolveRun(topology, biome, rng, topology.CellCount);
        PrepareForSolve(topology);

        // Initial propagation to apply any pre-existing constraints
        var initialResult = _propagator.PropagateAll(topology);
        if (!initialResult.Success)
        {
            return WfcSolveResult.Failed(
                "Initial propagation found contradiction",
                0,
                initialResult.ContradictionCellId,
                StepTimings());
        }

        var iterations = 0;
        while (!topology.IsFullyCollapsed())
        {
            cancellationToken.ThrowIfCancellationRequested();
            iterations++;

            if (iterations > MaxIterations)
                return FailIterationBudget(iterations, run.TotalCells);

            LogProgress(iterations, run.TotalCells);

            var outcome = CollapseNextCell(run, iterations);
            if (outcome.Status == WfcStepStatus.Complete)
                break;

            if (outcome.Status == WfcStepStatus.Failed)
                return outcome.Failure.WithTimings(StepTimings());
        }

        ILog.Print($"[WFC] Success! Completed {iterations} iterations for {run.TotalCells} cells");
        return WfcSolveResult.Succeeded(iterations, StepTimings());
    }

    /// <summary>The per-cell step totals since the solve began; WfcMapGenerator adds solve and conversion.</summary>
    private WfcStepTimings StepTimings() => new(
        Stopwatch.GetElapsedTime(0, _propagationTicks),
        Stopwatch.GetElapsedTime(0, _tileSelectionTicks),
        Stopwatch.GetElapsedTime(0, _cellCollapseTicks),
        TimeSpan.Zero,
        TimeSpan.Zero);

    /// <summary>
    /// Clears per-solve state and registers the constraints that invalidate cells beyond neighbors.
    /// </summary>
    private void PrepareForSolve(IWfcTopology topology)
    {
        _collapseEffects.Reset(topology);
        _entropyCache.Reset();
        _propagationTicks = 0;
        _tileSelectionTicks = 0;
        _cellCollapseTicks = 0;

        _entropyCache.ClearInvalidators();
        foreach (var constraint in _selector.GetConstraints())
        {
            if (constraint is IEntropyInvalidator invalidator)
                _entropyCache.RegisterInvalidator(invalidator);
        }
    }

    private WfcSolveResult FailIterationBudget(int iterations, int totalCells)
    {
        ILog.Print($"[WFC] Exceeded MaxIterations at {iterations}, collapsed ~{iterations}/{totalCells}");
        return WfcSolveResult.Failed(
            $"Exceeded maximum iterations ({MaxIterations})",
            iterations,
            timings: StepTimings());
    }

    private static void LogProgress(int iterations, int totalCells)
    {
        // Progress logging every 100 iterations (iterations ~ collapsed cells)
        if (iterations % 100 == 0)
            ILog.Print($"[WFC] Progress: {iterations}/{totalCells} cells ({100 * iterations / totalCells}%)");
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
            ILog.Print($"[WFC] Contradiction at cell {targetCellId} after {iterations}/{run.TotalCells} cells");
            return WfcStepOutcome.Failed(WfcSolveResult.Failed(
                "Found cell with no valid options",
                iterations,
                targetCellId));
        }

        var selectedTile = SelectTileFor(run, targetCellId.Value);
        if (selectedTile == null)
        {
            ILog.Print(
                $"[WFC] Selector returned null at cell {targetCellId} after " +
                $"{iterations}/{run.TotalCells} cells, validTiles={targetCell.GetPossibleTiles().Count}");
            return WfcStepOutcome.Failed(WfcSolveResult.Failed(
                "Tile selector returned null",
                iterations,
                targetCellId));
        }

        CollapseCell(topology, targetCellId.Value, selectedTile);

        // Propagate constraints to neighbors
        var propagationStart = Stopwatch.GetTimestamp();
        var propResult = _propagator.Propagate(topology, targetCellId.Value);
        _propagationTicks += Stopwatch.GetTimestamp() - propagationStart;

        if (propResult.Success)
            return WfcStepOutcome.Collapsed;

        ILog.Print(
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

        var selectionStart = Stopwatch.GetTimestamp();
        var tile = _selector.SelectTile(
            run.Topology.GetCell(cellId).GetPossibleTiles(),
            run.Biome,
            run.Rng,
            continuityTiles,
            cellId,
            run.Topology);
        _tileSelectionTicks += Stopwatch.GetTimestamp() - selectionStart;
        return tile;
    }

    private void CollapseCell(IWfcTopology topology, int cellId, string tileId)
    {
        var collapseStart = Stopwatch.GetTimestamp();
        topology.GetCell(cellId).CollapseTo(tileId);
        _collapseEffects.Apply(topology, cellId, tileId);

        // Mark affected cells dirty for entropy recalculation
        _entropyCache.OnCellCollapsed(cellId, tileId, topology);
        _cellCollapseTicks += Stopwatch.GetTimestamp() - collapseStart;
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
