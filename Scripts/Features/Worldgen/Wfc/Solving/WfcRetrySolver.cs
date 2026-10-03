using System;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;
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
    /// <returns>Named result containing the solve result and last attempt's topology</returns>
    internal static WfcSolveAttempt SolveWithRetry(
        WfcSolver solver,
        Func<IWfcTopology> createTopology,
        BiomeDefinition? biome,
        ulong baseSeed,
        int maxRetries)
    {
        IWfcTopology? lastTopology = null;
        WfcSolveResult lastResult = default;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            var topology = createTopology();
            lastTopology = topology;

            var rng = new RandomNumberGenerator();
            rng.Seed = baseSeed + (ulong)attempt;

            lastResult = solver.Solve(topology, biome, rng);

            if (lastResult.Success)
                return new WfcSolveAttempt(lastResult, topology);
        }

        return new WfcSolveAttempt(
            WfcSolveResult.Failed(
                $"All {maxRetries + 1} attempts failed. Last error: {lastResult.ErrorMessage}",
                lastResult.Iterations,
                lastResult.ContradictionCellId),
            lastTopology!);
    }
}
