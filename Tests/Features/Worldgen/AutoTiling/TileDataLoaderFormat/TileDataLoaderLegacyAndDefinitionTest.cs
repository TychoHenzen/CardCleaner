using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TileDataLoaderFormat;

/// <summary>
///     TileDataLoaderLegacyAndDefinitionTest scenarios split out of TileDataLoaderFormatTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileDataLoaderLegacyAndDefinitionTest : TileDataLoaderFormatTestBase
{
    // ==================== Legacy Compatibility ====================

    /// <summary>
    /// Test legacy tiles.json without new sections still loads.
    /// </summary>
    [TestCase]
    public void TestLegacy_NoNewSectionsLoadsCorrectly()
    {
        var json = """
        {
          "version": "1.0",
          "tileset": "res://Assets/test.tres",
          "tiles": [
            {"id": "grass", "name": "Grass", "passability": "passable", "atlasCoords": {"x": 0, "y": 0}},
            {"id": "dirt", "name": "Dirt", "passability": "passable", "atlasCoords": {"x": 1, "y": 0}}
          ]
        }
        """;
        var result = LoadFromJson(json);

        AssertThat(result.Tiles.Count).IsEqual(2);
        AssertThat(result.TilesetConfig).IsNotNull();
        AssertThat(result.TilesetConfig.BaseTileSize).IsEqual(new Vector2I(16, 16));
    }

    /// <summary>
    /// Test that autoTileFormat on tiles uses registry lookup.
    /// </summary>
    [TestCase]
    public void TestTileDefinition_AutoTileFormatFromRegistry()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "custom_format_xyz",
              "bitmaskType": "edge4",
              "variants": [
                {"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}},
                {"bitmask": 5, "atlasCoords": {"x": 5, "y": 0}}
              ]
            }
          ],
          "tiles": [
            {
              "id": "custom_tile",
              "name": "Custom Tile",
              "passability": "passable",
              "atlasCoords": {"x": 0, "y": 0},
              "autoTileFormat": "custom_format_xyz"
            }
          ]
        }
        """;
        var result = LoadFromJson(json);

        var tile = result.Tiles[0];
        var format = tile.GetAutoTileFormat();

        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("custom_format_xyz");
        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Edge4);
    }

    /// <summary>
    /// Test tile with built-in format name uses built-in.
    /// </summary>
    [TestCase]
    public void TestTileDefinition_BuiltInFormatUsed()
    {
        var json = """
        {
          "tiles": [
            {
              "id": "blob_tile",
              "name": "Blob Tile",
              "passability": "passable",
              "atlasCoords": {"x": 0, "y": 0},
              "autoTileFormat": "blob47"
            }
          ]
        }
        """;
        var result = LoadFromJson(json);

        var tile = result.Tiles[0];
        var format = tile.GetAutoTileFormat();

        AssertThat(format).IsNotNull();
        AssertString(format!.Name).IsEqual("blob47");
        AssertBool(format.IsBuiltIn).IsTrue();
        AssertThat(format.GetExpectedVariantCount()).IsEqual(47);
    }

    // ==================== Error Handling ====================

    /// <summary>
    /// Test format without name is skipped.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_MissingNameSkipped()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "bitmaskType": "edge4",
              "variants": [{"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}}]
            },
            {
              "name": "valid_format",
              "bitmaskType": "edge4",
              "variants": [{"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}}]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        // Only the valid format should be registered
        AssertBool(AutoTileFormatRegistry.Contains("valid_format")).IsTrue();
        // Invalid format with no name shouldn't cause issues
    }

    /// <summary>
    /// Test format without variants is skipped.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_EmptyVariantsSkipped()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "empty_variants",
              "bitmaskType": "edge4",
              "variants": []
            },
            {
              "name": "valid_format_2",
              "bitmaskType": "edge4",
              "variants": [{"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}}]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        AssertBool(AutoTileFormatRegistry.Contains("empty_variants")).IsFalse();
        AssertBool(AutoTileFormatRegistry.Contains("valid_format_2")).IsTrue();
    }
}
