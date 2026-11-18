using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TileDataLoaderFormat;

/// <summary>
///     TileDataLoaderFormatParsingTest scenarios split out of TileDataLoaderFormatTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileDataLoaderFormatParsingTest : TileDataLoaderFormatTestBase
{
    // ==================== AutoTileFormats Parsing ====================

    /// <summary>
    /// Test that autoTileFormats section registers custom formats.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_RegistersCustomFormat()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "test_hedge4",
              "bitmaskType": "edge4",
              "variants": [
                {"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}},
                {"bitmask": 1, "atlasCoords": {"x": 1, "y": 0}},
                {"bitmask": 2, "atlasCoords": {"x": 2, "y": 0}}
              ]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        AssertBool(AutoTileFormatRegistry.Contains("test_hedge4")).IsTrue();
        var format = AutoTileFormatRegistry.Get("test_hedge4");
        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Edge4);
        AssertBool(format.IsBuiltIn).IsFalse();
        AssertThat(format.AllowedBitmasks.Count).IsEqual(3);
    }

    /// <summary>
    /// Test that custom format with Full8 bitmask type is parsed.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_Full8Type()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "test_blob_custom",
              "bitmaskType": "full8",
              "variants": [
                {"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}},
                {"bitmask": 255, "atlasCoords": {"x": 1, "y": 0}}
              ]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        var format = AutoTileFormatRegistry.Get("test_blob_custom");
        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Full8);
    }

    /// <summary>
    /// Test that Corner4 is default when bitmaskType not specified.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_DefaultsToCorner4()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "test_default_type",
              "variants": [
                {"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}}
              ]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        var format = AutoTileFormatRegistry.Get("test_default_type");
        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Corner4);
    }

    /// <summary>
    /// Test variant with Size and Offset is parsed.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_VariantWithSizeAndOffset()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "test_tall_variant",
              "bitmaskType": "edge4",
              "variants": [
                {"bitmask": 4, "atlasCoords": {"x": 0, "y": 5}, "size": {"x": 1, "y": 3}, "offset": {"x": 0, "y": -2}}
              ]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        var format = AutoTileFormatRegistry.Get("test_tall_variant");
        var variant = format.GetVariant(4);

        AssertThat(variant).IsNotNull();
        AssertThat(variant!.Value.AtlasCoords).IsEqual(new Vector2I(0, 5));
        AssertThat(variant!.Value.Size).IsEqual(new Vector2I(1, 3));
        AssertThat(variant!.Value.Offset).IsEqual(new Vector2I(0, -2));
    }

    /// <summary>
    /// Test variant with AtlasRegionSize is parsed.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_VariantWithAtlasRegionSize()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "test_region_size",
              "bitmaskType": "edge4",
              "variants": [
                {"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}, "atlasRegionSize": {"x": 32, "y": 48}}
              ]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        var format = AutoTileFormatRegistry.Get("test_region_size");
        var variant = format.GetVariant(0);

        AssertThat(variant).IsNotNull();
        AssertThat(variant!.Value.AtlasRegionSize).IsNotNull();
        AssertThat(variant!.Value.AtlasRegionSize!.Value).IsEqual(new Vector2I(32, 48));
    }

    /// <summary>
    /// Test that missing autoTileFormats section doesn't cause errors.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_MissingIsOk()
    {
        var json = """
        {
          "tiles": [
            {"id": "grass", "name": "Grass", "passability": "passable", "atlasCoords": {"x": 0, "y": 0}}
          ]
        }
        """;
        var result = LoadFromJson(json);

        // Should load without errors and built-ins should still be available
        AssertThat(result.Tiles.Count).IsEqual(1);
        AssertBool(AutoTileFormatRegistry.Contains("corner16")).IsTrue();
    }

    /// <summary>
    /// Test that built-in format names cannot be overridden.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_CannotOverrideBuiltIn()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "corner16",
              "bitmaskType": "edge4",
              "variants": [
                {"bitmask": 0, "atlasCoords": {"x": 99, "y": 99}}
              ]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        // Built-in should still be the original
        var format = AutoTileFormatRegistry.Get("corner16");
        AssertBool(format.IsBuiltIn).IsTrue();
        AssertThat(format.BitmaskType).IsEqual(BitmaskType.Corner4);
    }

    /// <summary>
    /// Test multiple custom formats can be registered.
    /// </summary>
    [TestCase]
    public void TestAutoTileFormats_MultipleFormats()
    {
        var json = """
        {
          "autoTileFormats": [
            {
              "name": "format_a",
              "bitmaskType": "edge4",
              "variants": [{"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}}]
            },
            {
              "name": "format_b",
              "bitmaskType": "corner4",
              "variants": [{"bitmask": 0, "atlasCoords": {"x": 0, "y": 0}}]
            }
          ],
          "tiles": []
        }
        """;
        LoadFromJson(json);

        AssertBool(AutoTileFormatRegistry.Contains("format_a")).IsTrue();
        AssertBool(AutoTileFormatRegistry.Contains("format_b")).IsTrue();
    }
}
