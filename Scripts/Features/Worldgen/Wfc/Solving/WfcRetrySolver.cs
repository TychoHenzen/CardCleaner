using System;
using System.Collections.Generic;
using System.Threading;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;
using CardCleaner.Scripts.Core.Randomness;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Runs a solver repeatedly on fresh topologies until one attempt succeeds.
/// Each retry uses a different random seed.
/// </summary>
internal static class WfcRetrySolver
{
    /// <summary>
    /// Attempts to solve with automatic retry on contradiction.
    /// </summary>
    /// <param name="solver">Solver used for every attempt</param>
    /// <param name="createTopology">Factory function to create a fresh topology</param>
    /// <param name="biome">Biome for soft rule weighting</param>
    /// <param name="baseSeed">Base seed for random number generation</param>
    /// <param name="maxRetries">Maximum retry attempts</param>
    /// <param name="cancellationToken">Passed to every attempt's solve</param>
    /// <returns>Named result containing the solve result and last attempt's topology</returns>
    /// <exception cref="OperationCanceledException">Thrown when the token is cancelled during a solve.</exception>
    internal static WfcSolveAttempt SolveWithRetry(
        WfcSolver solver,
        Func<IWfcTopology> createTopology,
        BiomeDefinition? biome,
        ulong baseSeed,
        int maxRetries,
        CancellationToken cancellationToken = default)
    {
        IWfcTopology? lastTopology = null;
        WfcSolveResult lastResult = default;
        var timings = WfcStepTimings.Zero;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var topology = createTopology();
            lastTopology = topology;

            var rng = RandomSources.Create();
            rng.Seed = baseSeed + (ulong)attempt;

            lastResult = solver.Solve(topology, biome, rng, cancellationToken);
            // ASSUMPTION: failed attempts' time counts toward the solve's totals, so the totals cover all the work
            // of one solve call.
            timings += lastResult.Timings;

            if (lastResult.Success)
                return new WfcSolveAttempt(lastResult.WithTimings(timings), topology);
        }

        return new WfcSolveAttempt(
            WfcSolveResult.Failed(
                $"All {maxRetries + 1} attempts failed. Last error: {lastResult.ErrorMessage}",
                lastResult.Iterations,
                lastResult.ContradictionCellId,
                timings),
            lastTopology!);
    }

    /// <summary>
    /// Solves a graph of cells once with the graph constraints and a fresh rule set.
    /// </summary>
    internal static WfcGraphSolution SolveGraph(
        int[][] neighbors,
        IReadOnlyList<(string tileA, string tileB)> transitionPairs,
        IWfcTileCatalog catalog,
        IReadOnlyCollection<string> initialTiles,
        ulong seed,
        Func<string, bool>? gapTileFilter)
    {
        var topology = new WfcNeighborListTopology(neighbors, initialTiles);
        var rules = new WfcAdjacencyRules(transitionPairs);
        GapTileAdjacencyConfigurator.Configure(rules, catalog, gapTileFilter, logSummary: false);

        var blobTracker = new BlobSizeTracker();
        blobTracker.Initialize(neighbors.Length);
        var coherence = new SpatialCoherenceConstraint(catalog);
        var solver = CreateGraphSolver(rules, catalog, blobTracker, coherence, neighbors.Length);

        var result = solver.Solve(topology, null, RandomSources.Create(seed));
        return ToGraphSolution(result, topology, neighbors.Length);
    }

    private static WfcSolver CreateGraphSolver(
        WfcAdjacencyRules rules,
        IWfcTileCatalog catalog,
        BlobSizeTracker blobTracker,
        SpatialCoherenceConstraint coherence,
        int cellCount)
    {
        var selector = new WfcTileSelector();
        selector.AddConstraint(new DiminishingReturnsSoftModifier(blobTracker));
        selector.AddConstraint(coherence);
        selector.AddConstraint(new AutoTileGapConstraint(catalog));
        selector.AddConstraint(new NoSolidFillConstraint(catalog));
        selector.AddConstraint(new TileProbabilityConstraint(catalog));

        return new WfcSolver(
            new WfcPropagator(rules),
            selector,
            blobTracker,
            spatialCoherence: coherence,
            tileCatalog: catalog)
        {
            MaxIterations = cellCount * 2,
        };
    }

    private static WfcGraphSolution ToGraphSolution(WfcSolveResult result, IWfcTopology topology, int cellCount)
    {
        var collapsedTiles = new string?[cellCount];
        for (var cellId = 0; cellId < cellCount; cellId++)
            collapsedTiles[cellId] = topology.GetCollapsedTileAt(cellId);

        return new WfcGraphSolution(result.Success, result.Iterations, result.ErrorMessage, collapsedTiles);
    }
}
