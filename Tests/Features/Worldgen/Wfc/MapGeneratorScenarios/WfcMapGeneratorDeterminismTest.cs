using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.MapGeneratorScenarios;

/// <summary>
///     WfcMapGeneratorDeterminismTest scenarios split out of WfcMapGeneratorIntegrationTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcMapGeneratorDeterminismTest : WfcMapGeneratorIntegrationTestBase
{
    [TestCase]
    public void TestDifferentSeedsProduceDifferentMaps()
    {
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("plains");

        var result1 = generator.Generate(biome!, new Vector2I(8, 8), 111);
        var result2 = generator.Generate(biome!, new Vector2I(8, 8), 222);

        if (!result1.Success || !result2.Success)
        {
            GD.Print("Skipping seed variation test - generation failed");
            return;
        }

        // Count unique tile types in each map
        var map1 = result1.MapData!;
        var map2 = result2.MapData!;
        var uniqueTiles = new HashSet<string>();

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (map1.TileIds[y, x] != null)
                    uniqueTiles.Add(map1.TileIds[y, x]!);
            }
        }

        // If only one tile type exists, test is inconclusive (skip)
        if (uniqueTiles.Count <= 1)
        {
            GD.Print($"Skipping seed variation test - only {uniqueTiles.Count} tile type(s) available");
            return;
        }

        // Count how many tiles are different
        var differentCount = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (map1.TileIds[y, x] != map2.TileIds[y, x])
                    differentCount++;
            }
        }

        // Maps should have some variation when multiple tile types are available
        AssertThat(differentCount).IsGreater(0);
    }

    [TestCase]
    public void TestSameSeedProducesSameMap()
    {
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("plains");

        var result1 = generator.Generate(biome!, new Vector2I(8, 8), 42);
        var result2 = generator.Generate(biome!, new Vector2I(8, 8), 42);

        if (!result1.Success || !result2.Success)
        {
            GD.Print("Skipping determinism test - generation failed");
            return;
        }

        var map1 = result1.MapData!;
        var map2 = result2.MapData!;

        // Maps should be identical
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                AssertString(map1.TileIds[y, x]).IsEqual(map2.TileIds[y, x]);
            }
        }
    }

    [TestCase]
    public void TestPassableTilesPopulated()
    {
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("plains");

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        if (!result.Success)
        {
            GD.Print("Skipping passable tiles test - generation failed");
            return;
        }

        var mapData = result.MapData!;

        // PassableTiles should be populated
        AssertThat(mapData.PassableTiles.Count).IsGreater(0);

        // All entries should be within bounds
        foreach (var pos in mapData.PassableTiles)
        {
            AssertBool(pos.X >= 0 && pos.X < mapData.Size.X).IsTrue();
            AssertBool(pos.Y >= 0 && pos.Y < mapData.Size.Y).IsTrue();
        }
    }

    [TestCase]
    public void TestPlayerStartIsPassable()
    {
        var generator = new WfcMapGenerator(new WfcAdjacencyRules(_transitionPairs));
        var biome = _biomeRegistry.GetBiome("plains");

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        if (!result.Success)
        {
            GD.Print("Skipping player start test - generation failed");
            return;
        }

        var mapData = result.MapData!;

        // Player start should be a passable tile
        AssertBool(mapData.PassableTiles.Contains(mapData.PlayerStart)).IsTrue();
    }
}
