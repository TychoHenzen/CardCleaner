using System.IO;
using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Worldgen.Biomes;

/// <summary>
/// BiomeDataLoader reads only the biomes section of tiles.json and returns no biomes when it cannot read them.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class BiomeDataLoaderTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TestDefaultPathLoadsTheDataFileBiomes()
    {
        var biomes = BiomeDataLoader.LoadBiomes(new GodotAssetPaths());

        AssertThat(biomes.Count).IsEqual(10);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMissingFileReturnsNoBiomes()
    {
        var biomes = BiomeDataLoader.LoadBiomes(new GodotAssetPaths(), "res://Data/Tiles/missing-biomes.json");

        AssertThat(biomes.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestFakeAssetPathsLoadsSameBiomesAsDefault()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var tilesDir = Path.Combine(root, "Data", "Tiles");
            Directory.CreateDirectory(tilesDir);
            File.Copy(new GodotAssetPaths().Globalize("res://Data/Tiles/tiles.json"),
                Path.Combine(tilesDir, "tiles.json"));

            var fromFake = BiomeDataLoader.LoadBiomes(new TempAssetPaths(root));
            var fromGodot = BiomeDataLoader.LoadBiomes(new GodotAssetPaths());

            AssertThat(fromFake.Count).IsEqual(10);
            AssertThat(string.Join(",", fromFake.Keys.OrderBy(key => key)))
                .IsEqual(string.Join(",", fromGodot.Keys.OrderBy(key => key)));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestFakeRootWithoutFileReturnsNoBiomes()
    {
        var root = TempAssetPaths.CreateTempRoot();
        try
        {
            var biomes = BiomeDataLoader.LoadBiomes(new TempAssetPaths(root));

            AssertThat(biomes.Count).IsEqual(0);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMissingBiomesSectionReturnsNoBiomes()
    {
        var biomes = BiomeDataLoader.ParseBiomes("{ \"tiles\": [] }");

        AssertThat(biomes.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMalformedJsonReturnsNoBiomes()
    {
        var biomes = BiomeDataLoader.ParseBiomes("{ \"biomes\": { \"plains\": ");

        AssertThat(biomes.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestCommentsTrailingCommasAndKeyCasingAreAccepted()
    {
        const string json = """
            {
              // Comments are skipped
              "biomes": {
                "test_biome": {
                  "DisplayName": "Test",
                  "signature": [0, 0, 0, 0, 0, 0, 0, 0],
                  "BlockedPercentage": 0.25,
                  "passableTiles": { "grass": 1.0, },
                  "blockedTiles": { "rock": 1.0, },
                },
              },
            }
            """;

        var biomes = BiomeDataLoader.ParseBiomes(json);

        AssertThat(biomes.Count).IsEqual(1);
        AssertThat(biomes["test_biome"].DisplayName).IsEqual("Test");
        AssertThat(biomes["test_biome"].BlockedPercentage).IsEqual(0.25f);
        AssertThat(biomes["test_biome"].PassableTiles.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMalformedTileSectionDoesNotHideBiomes()
    {
        const string json = """
            { "tiles": "not-a-list", "biomes": { "plains": { "displayName": "Plains" } } }
            """;

        var biomes = BiomeDataLoader.ParseBiomes(json);

        AssertThat(biomes.Count).IsEqual(1);
    }
}
