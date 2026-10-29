using System;
using System.Diagnostics;
using System.Threading;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Contracts;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Runs a retrying WFC solve and turns its outcome into a <see cref="WfcGenerationResult"/>, for
/// <see cref="WfcMapGenerator"/>. It holds no state: the generator passes in everything a solve needs.
/// </summary>
internal static class WfcSolveRunner
{
    /// <summary>
    /// Runs the retrying solve and adds the time of the whole call, every attempt included, to the result's Solve
    /// timing.
    /// </summary>
    public static WfcSolveAttempt SolveTimed(
        WfcSolver solver,
        Func<IWfcTopology> createGrid,
        BiomeDefinition? biome,
        ulong seed,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        var start = Stopwatch.GetTimestamp();
        var attempt = WfcRetrySolver.SolveWithRetry(solver, createGrid, biome, seed, maxRetries, cancellationToken);
        var timings = attempt.Result.Timings with { Solve = Stopwatch.GetElapsedTime(start) };
        return attempt with { Result = attempt.Result.WithTimings(timings) };
    }

    /// <summary>
    /// Turns a solve attempt into a generation result: a failed solve fails with zero timings, a solved grid is
    /// converted to tile ids and a biome map, with the conversion time added to the solve's timings.
    /// </summary>
    public static WfcGenerationResult ToGenerationResult(
        WfcSolveAttempt attempt,
        Vector2I size,
        Func<Vector2I, BiomeDefinition> getBiomeAt)
    {
        var solveResult = attempt.Result;
        if (!solveResult.Success)
        {
            return WfcGenerationResult.Failed(solveResult.ErrorMessage ?? "Unknown error");
        }

        var grid = (WfcGrid)attempt.Topology;
        var conversionStart = Stopwatch.GetTimestamp();
        var biomeMap = BuildBiomeMap(size, getBiomeAt);
        var tileIds = WfcGridTileIds.ToTileIds(grid);
        var timings = solveResult.Timings with { MapDataConversion = Stopwatch.GetElapsedTime(conversionStart) };
        return WfcGenerationResult.Succeeded(
            new Vector2I(grid.Width, grid.Height), tileIds, biomeMap, solveResult.Iterations, timings);
    }

    private static string[,] BuildBiomeMap(Vector2I size, Func<Vector2I, BiomeDefinition> getBiomeAt)
    {
        var biomeMap = new string[size.Y, size.X];
        for (var y = 0; y < size.Y; y++)
        {
            for (var x = 0; x < size.X; x++)
            {
                biomeMap[y, x] = getBiomeAt(new Vector2I(x, y)).Id;
            }
        }
        return biomeMap;
    }
}
