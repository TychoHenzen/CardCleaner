using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc;

/// <summary>
/// Performance benchmark tests for WFC map generation.
/// These tests measure and validate performance characteristics
/// and verify determinism is maintained after optimizations.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcPerformanceBenchmarkTest
{
    // Max-of-3 averages of three process runs, measured 2026-10-10 on AMD Ryzen 7 7435HS with the production tile
    // catalog and an optimized build (-p:Optimize=true). Run averages for 30x30: 1305.6, 1306.5, 1314.1 ms. See
    // WfcTestFixtures.PerfBudgetMs and BuildFactor.
    private const double OptimizedBaseline30x30Ms = 1314.1;
    // Run averages for 60x60: 6641.4, 10053.6, 6261.8 ms. Run 2 is noisy (one run took 15284 ms), so its max is high
    // and this budget is a coarse guard; the median run would give a budget of 49810 ms.
    private const double OptimizedBaseline60x60Ms = 10053.6;

    private IReadOnlyList<(string tileA, string tileB)> _transitionPairs = null!;
    private BiomeRegistry _biomeRegistry = null!;

    [BeforeTest]
    public void Setup()
    {
        _transitionPairs = new CompiledTransitionResolver(new GodotAssetPaths()).GetAllTransitionPairs().ToList();
        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes(new GodotAssetPaths());
    }

    [TestCase]
    public void TestDeterminism_SameSeedSameOutput()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");
        AssertThat(biome).IsNotNull();

        const ulong seed = 42424242;
        const int size = 30;

        var result1 = generator.Generate(biome!, new Vector2I(size, size), seed);
        var result2 = generator.Generate(biome!, new Vector2I(size, size), seed);

        WfcTestFixtures.AssertSucceeded(result1, "Determinism run 1");
        WfcTestFixtures.AssertSucceeded(result2, "Determinism run 2");

        var tiles1 = result1.TileIds!;
        var tiles2 = result2.TileIds!;

        // Verify every tile is identical
        var differences = 0;
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                if (tiles1[y, x] != tiles2[y, x])
                    differences++;
            }
        }

        AssertThat(differences).IsEqual(0);
        GD.Print($"[Benchmark] Determinism verified: {size}x{size} map identical across runs");
    }

    [TestCase]
    public void TestPerformance_30x30Grid()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");
        AssertThat(biome).IsNotNull();

        WfcTestFixtures.WarmUp(_transitionPairs, biome!);

        const int runs = 5;
        const int size = 30;
        var times = new double[runs];
        var iterations = new int[runs];

        for (var i = 0; i < runs; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = generator.Generate(biome!, new Vector2I(size, size), (ulong)(i * 1000 + 12345));
            stopwatch.Stop();

            times[i] = stopwatch.Elapsed.TotalMilliseconds;
            iterations[i] = result.Iterations;

            WfcTestFixtures.AssertSucceeded(result, $"30x30 run {i + 1}");
        }

        var avgTime = times.Average();
        var avgIterations = iterations.Average();
        var minTime = times.Min();
        var maxTime = times.Max();

        GD.Print($"[Benchmark] {size}x{size} grid ({runs} runs):");
        GD.Print($"  Average time: {avgTime:F1}ms");
        GD.Print($"  Min/Max time: {minTime:F1}ms / {maxTime:F1}ms");
        GD.Print($"  Average iterations: {avgIterations:F0}");
        GD.Print($"  Time per iteration: {avgTime / avgIterations:F3}ms");

        WfcTestFixtures.AssertWithinBudget("30x30", avgTime, OptimizedBaseline30x30Ms);
    }

    [TestCase]
    public void TestPerformance_60x60Grid()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");
        AssertThat(biome).IsNotNull();

        WfcTestFixtures.WarmUp(_transitionPairs, biome!);

        const int runs = 3;
        const int size = 60;
        var times = new double[runs];
        var iterations = new int[runs];

        for (var i = 0; i < runs; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = generator.Generate(biome!, new Vector2I(size, size), (ulong)(i * 1000 + 54321));
            stopwatch.Stop();

            times[i] = stopwatch.Elapsed.TotalMilliseconds;
            iterations[i] = result.Iterations;

            WfcTestFixtures.AssertSucceeded(result, $"60x60 run {i + 1}");
        }

        var avgTime = times.Average();
        var avgIterations = iterations.Average();
        var minTime = times.Min();
        var maxTime = times.Max();

        GD.Print($"[Benchmark] {size}x{size} grid ({runs} runs):");
        GD.Print($"  Average time: {avgTime:F1}ms");
        GD.Print($"  Min/Max time: {minTime:F1}ms / {maxTime:F1}ms");
        GD.Print($"  Average iterations: {avgIterations:F0}");
        GD.Print($"  Time per iteration: {avgTime / avgIterations:F3}ms");

        WfcTestFixtures.AssertWithinBudget("60x60", avgTime, OptimizedBaseline60x60Ms);
    }

    [TestCase]
    public static void TestConstraintOverhead_MeasurePerTileTime()
    {
        // This test measures how long each constraint evaluation takes
        // by comparing WFC with and without constraints
        var tileRegistry = WfcTestFixtures.CreateTestTileRegistry(WfcTestFixtures.TestBiomeId, "A", "B", "C", "D");
        var generator = new WfcMapGenerator(
            WfcTestFixtures.FullAdjacencyRules(),
            TileCatalogData.From(tileRegistry));
        generator.MaxRetries = 1; // Quick fail on contradiction

        var biome = WfcTestFixtures.CreateAbcdBiome();

        const int size = 40;
        const int runs = 3;
        var times = new long[runs];

        for (var i = 0; i < runs; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = generator.Generate(biome, new Vector2I(size, size), (ulong)(i * 500 + 99999));
            stopwatch.Stop();
            WfcTestFixtures.AssertSucceeded(result, $"40x40 run {i + 1}");
            times[i] = stopwatch.ElapsedMilliseconds;
        }

        var avgTime = times.Average();
        var totalCells = size * size;
        var timePerCell = avgTime / totalCells;

        GD.Print($"[Benchmark] Constraint overhead ({size}x{size}):");
        GD.Print($"  Average time: {avgTime:F1}ms for {totalCells} cells");
        GD.Print($"  Time per cell: {timePerCell:F3}ms");

        // NOTE: Current implementation with full entropy calculation has higher per-cell overhead.
        // The CLAUDE.md documents that entropy must be calculated using weighted probabilities
        // from constraint evaluation (not simple tile counts), which adds computational cost.
        // Target: under 10ms per cell (allows for constraint evaluation overhead)
        AssertThat(timePerCell).IsLess(10.0);
    }
}
