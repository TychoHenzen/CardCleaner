using System.Diagnostics;
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

    [TestCase]
    public void TestPerformanceUnder500ms()
    {
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");

        var stopwatch = Stopwatch.StartNew();
        var result = generator.Generate(biome!, new Vector2I(20, 20), 12345);
        stopwatch.Stop();

        GD.Print($"WFC generation for 20x20: {stopwatch.ElapsedMilliseconds}ms, {result.Iterations} iterations");

        WfcTestFixtures.AssertSucceeded(result, "Plains 20x20 map");
        AssertThat(stopwatch.ElapsedMilliseconds).IsLess(500);
    }
}
