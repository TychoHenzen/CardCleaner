using System;
using System.Diagnostics;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.MapGeneratorScenarios;

/// <summary>
///     WfcMapGeneratorValidityTest scenarios split out of WfcMapGeneratorIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcMapGeneratorValidityTest : WfcMapGeneratorIntegrationTestBase
{
    [TestCase]
    public void TestGeneratesValidMapWithForestBiome()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("forest");

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        WfcTestFixtures.AssertSucceeded(result, "Forest 10x10 map");
        AssertThat(result.TileIds).IsNotNull();
        AssertThat(result.Size).IsEqual(new Vector2I(10, 10));
    }

    [TestCase]
    public void TestGeneratesValidMapWithPlainsBiome()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        WfcTestFixtures.AssertSucceeded(result, "Plains 10x10 map");
        AssertThat(result.TileIds).IsNotNull();
    }

    [TestCase]
    public void TestNoInvalidAdjacencies()
    {
        // WfcMapGenerator adds the gap-tile adjacencies to the rules instance it is given, so the rules checked
        // below are the ones the solver enforced. ASSUMPTION: a fresh WfcAdjacencyRules would miss those additions.
        var adjacencyRules = new WfcAdjacencyRules(_transitionPairs);
        var generator = new WfcMapGenerator(adjacencyRules, WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");

        var result = generator.Generate(biome!, new Vector2I(8, 8), 42);

        WfcTestFixtures.AssertSucceeded(result, "Plains 8x8 map");

        var tileIds = result.TileIds!;

        // Check all horizontal adjacencies
        for (var y = 0; y < result.Size.Y; y++)
        {
            for (var x = 0; x < result.Size.X - 1; x++)
            {
                var tileA = tileIds[y, x];
                var tileB = tileIds[y, x + 1];

                AssertBool(adjacencyRules.CanBeAdjacent(tileA, tileB)).IsTrue();
            }
        }

        // Check all vertical adjacencies
        for (var y = 0; y < result.Size.Y - 1; y++)
        {
            for (var x = 0; x < result.Size.X; x++)
            {
                var tileA = tileIds[y, x];
                var tileB = tileIds[y + 1, x];

                AssertBool(adjacencyRules.CanBeAdjacent(tileA, tileB)).IsTrue();
            }
        }
    }

    // Optimized max-of-3 average, measured 2026-10-10 on 51c8d93 plus the step 3 edits: run averages 760.8, 710.7 and
    // 736.0 ms. See WfcTestFixtures.PerfBudgetMs.
    private const double OptimizedBaseline20x20Ms = 760.8;

    [TestCase]
    public void TestPerformance20x20WithinBudget()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");
        AssertThat(biome).IsNotNull();
        WfcTestFixtures.WarmUp(_transitionPairs, biome!);

        const int runs = 3;
        var times = new double[runs];
        for (var i = 0; i < runs; i++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = generator.Generate(biome!, new Vector2I(20, 20), 12345);
            stopwatch.Stop();

            WfcTestFixtures.AssertSucceeded(result, "Plains 20x20 map");
            times[i] = stopwatch.Elapsed.TotalMilliseconds;
        }

        var avgTime = times.Average();
        GD.Print($"WFC generation for 20x20 ({runs} runs): average {avgTime:F1}ms, " +
                 $"min {times.Min():F1}ms, max {times.Max():F1}ms");
        WfcTestFixtures.AssertWithinBudget("20x20", avgTime, OptimizedBaseline20x20Ms);
    }
}
