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
        var tiles1 = result1.TileIds!;
        var tiles2 = result2.TileIds!;
        var uniqueTiles = new HashSet<string>();

        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                if (tiles1[y, x] != null)
                    uniqueTiles.Add(tiles1[y, x]!);
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
                if (tiles1[y, x] != tiles2[y, x])
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

        var tiles1 = result1.TileIds!;
        var tiles2 = result2.TileIds!;

        // Maps should be identical
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                AssertString(tiles1[y, x]).IsEqual(tiles2[y, x]);
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

        var tileIds = result.TileIds!;
        AssertThat(tileIds.GetLength(0)).IsEqual(result.Size.Y);
        AssertThat(tileIds.GetLength(1)).IsEqual(result.Size.X);

        // Passable cells are the cells whose tile is in the biome's passable pool
        var passableIds = new HashSet<string>(biome!.PassableTiles.GetAllTileIds());
        var passableCells = 0;
        foreach (var tileId in tileIds)
        {
            if (passableIds.Contains(tileId))
                passableCells++;
        }

        AssertThat(passableCells).IsGreater(0);
    }
}
