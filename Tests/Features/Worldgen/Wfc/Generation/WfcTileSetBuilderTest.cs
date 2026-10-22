using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Tests.Features.Worldgen.Support;

namespace CardCleaner.Tests.Features.Worldgen.Wfc.Generation;

/// <summary>
/// Pins the tile sets WfcTileSetBuilder collects: the rule tile ids the catalog contains and allows in the biome.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class WfcTileSetBuilderTest
{
    /// <summary>A catalog that allows tiles A and B in the test biome.</summary>
    private static TileCatalogData AbCatalog() =>
        TileCatalogData.From(WfcTestFixtures.CreateTestTileRegistry(WfcTestFixtures.TestBiomeId, "A", "B"));

    [TestCase]
    [TestCategory("Unit")]
    public static void ForBiomeKeepsRuleTilesTheCatalogAllowsInTheBiome()
    {
        var ruleIds = new HashSet<string> { "A", "B", "C" };
        var catalog = AbCatalog();

        var sets = new WfcTileSetBuilder(ruleIds, catalog).ForBiome(WfcTestFixtures.CreateAbcdBiome());

        AssertString(string.Join("|", sets.AllTiles.OrderBy(tileId => tileId)))
            .IsEqual("A|B");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void WithoutACatalogTheTileSetIsEmpty()
    {
        var ruleIds = new HashSet<string> { "A", "B", "C" };

        var sets = new WfcTileSetBuilder(ruleIds, null).ForBiome(WfcTestFixtures.CreateAbcdBiome());

        AssertInt(sets.AllTiles.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ARuleTileAddedLaterIsSeen()
    {
        var ruleIds = new HashSet<string> { "A" };
        var catalog = AbCatalog();
        var builder = new WfcTileSetBuilder(ruleIds, catalog);

        ruleIds.Add("B");
        var sets = builder.ForBiome(WfcTestFixtures.CreateAbcdBiome());

        AssertBool(sets.AllTiles.Contains("B")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ForAllBiomesAppliesTheTileFilter()
    {
        var ruleIds = new HashSet<string> { "A", "B" };
        var catalog = AbCatalog();
        var biomes = BiomeSet.From(new[] { WfcTestFixtures.CreateAbcdBiome() });

        var sets = new WfcTileSetBuilder(ruleIds, catalog).ForAllBiomes(biomes, tileId => tileId != "A");

        AssertString(string.Join("|", sets.AllTiles.OrderBy(tileId => tileId)))
            .IsEqual("B");
    }
}
