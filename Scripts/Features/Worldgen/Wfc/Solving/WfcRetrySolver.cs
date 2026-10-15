using System;
using System.Threading;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;
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
}
