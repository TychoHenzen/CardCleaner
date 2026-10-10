using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;
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
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");

        var result1 = generator.Generate(biome!, new Vector2I(8, 8), 111);
        var result2 = generator.Generate(biome!, new Vector2I(8, 8), 222);

        WfcTestFixtures.AssertSucceeded(result1, "Seed 111 map");
        WfcTestFixtures.AssertSucceeded(result2, "Seed 222 map");

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

        // One tile type would make the seed comparison meaningless, so the seed 111 map must show at least two
        AssertThat(uniqueTiles.Count)
            .OverrideFailureMessage($"Seed 111 map has {uniqueTiles.Count} tile type(s), need at least 2")
            .IsGreater(1);

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
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            WfcTestFixtures.ProductionCatalog());
        var biome = _biomeRegistry.GetBiome("plains");

        var result1 = generator.Generate(biome!, new Vector2I(8, 8), 42);
        var result2 = generator.Generate(biome!, new Vector2I(8, 8), 42);

        WfcTestFixtures.AssertSucceeded(result1, "Seed 42 map, first run");
        WfcTestFixtures.AssertSucceeded(result2, "Seed 42 map, second run");

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
        var catalog = WfcTestFixtures.ProductionCatalog();
        var generator = new WfcMapGenerator(
            new WfcAdjacencyRules(_transitionPairs),
            catalog);
        var biome = _biomeRegistry.GetBiome("plains");

        var result = generator.Generate(biome!, new Vector2I(10, 10), 12345);

        WfcTestFixtures.AssertSucceeded(result, "Seed 12345 map");

        var tileIds = result.TileIds!;
        AssertThat(tileIds.GetLength(0)).IsEqual(result.Size.Y);
        AssertThat(tileIds.GetLength(1)).IsEqual(result.Size.X);

        // The single-biome path weights tiles uniformly (WfcTileSelector, since 8788c0d), so the biome's passable
        // pool does not steer it. The map is checked against the registry's passability instead.
        var passableCells = 0;
        foreach (var tileId in tileIds)
        {
            if (catalog.IsPassable(tileId))
                passableCells++;
        }

        AssertThat(passableCells).IsGreater(0);
    }
}
