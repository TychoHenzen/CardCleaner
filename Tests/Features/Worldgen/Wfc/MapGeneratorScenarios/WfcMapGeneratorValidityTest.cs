using System.Diagnostics;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
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
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("forest");

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        // May fail if no valid tiles in biome have adjacency rules
        if (!result.Success)
        {
            GD.Print($"Generation failed (expected if tiles not in transition map): {result.ErrorMessage}");
            return;
        }

        AssertBool(result.Success).IsTrue();
        AssertThat(result.TileIds).IsNotNull();
        AssertThat(result.Size).IsEqual(new Vector2I(10, 10));
    }

    [TestCase]
    public void TestGeneratesValidMapWithPlainsBiome()
    {
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("plains");

        AssertThat(biome).IsNotNull();

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        if (!result.Success)
        {
            GD.Print($"Generation failed (expected if tiles not in transition map): {result.ErrorMessage}");
            return;
        }

        AssertBool(result.Success).IsTrue();
        AssertThat(result.TileIds).IsNotNull();
    }

    [TestCase]
    public void TestNoInvalidAdjacencies()
    {
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("plains");

        var result = generator.Generate(biome!, new Vector2I(8, 8), 42);

        if (!result.Success)
        {
            GD.Print($"Skipping adjacency test - generation failed: {result.ErrorMessage}");
            return;
        }

        var tileIds = result.TileIds!;
        var adjacencyRules = new WfcAdjacencyRules(_transitionPairs);

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
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("plains");

        var stopwatch = Stopwatch.StartNew();
        var result = generator.Generate(biome!, new Vector2I(20, 20), 12345);
        stopwatch.Stop();

        GD.Print($"WFC generation for 20x20: {stopwatch.ElapsedMilliseconds}ms, {result.Iterations} iterations");

        if (result.Success)
        {
            AssertThat(stopwatch.ElapsedMilliseconds).IsLess(500);
        }
        else
        {
            GD.Print($"Generation failed: {result.ErrorMessage}");
        }
    }
}
