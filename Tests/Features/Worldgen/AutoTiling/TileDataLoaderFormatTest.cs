using System.IO;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling;

/// <summary>
/// Tests for TileDataLoader's auto-tile format and tileset config parsing.
/// Verifies JSON serialization and deserialization of new format system features.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileDataLoaderFormatTest
{
    private string _testDir = null!;

    /// <summary>
    /// Setup test directory for JSON files.
    /// </summary>
    [BeforeTest]
    public void Setup()
    {
        // Use OS temp directory for test files
        _testDir = Path.Combine(Path.GetTempPath(), "cardcleaner_test_" + System.Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_testDir);
    }

    /// <summary>
    /// Clean up test directory and registry state.
    /// </summary>
    [AfterTest]
    public void Cleanup()
    {
        AutoTileFormatRegistry.ClearCustomFormats();

        if (Directory.Exists(_testDir))
        {
            try
            {
                Directory.Delete(_testDir, recursive: true);
            }
            catch
            {
                // Ignore cleanup errors in tests
            }
        }
    }

    // ==================== TilesetConfig Parsing ====================

    /// <summary>
    /// Test that missing tilesetConfig returns default values.
    /// </summary>
    [TestCase]
    public void TestTilesetConfig_MissingReturnsDefault()
    {
        var json = """
        {
          "tiles": [
            {"id": "grass", "name": "Grass", "passability": "passable", "atlasCoords": {"x": 0, "y": 0}}
          ]
        }
        """;
        var result = LoadFromJson(json);

        AssertThat(result.TilesetConfig).IsNotNull();
        AssertThat(result.TilesetConfig.BaseTileSize).IsEqual(new Vector2I(16, 16));
        AssertThat(result.TilesetConfig.GridOffset).IsEqual(Vector2.Zero);
    }

    /// <summary>
    /// Test that tilesetConfig section is parsed correctly.
    /// </summary>
    [TestCase]
    public void TestTilesetConfig_ParsedCorrectly()
    {
        var json = """
        {
          "tilesetConfig": {
            "baseTileSize": {"x": 24, "y": 24},
            "gridOffset": {"x": 0.5, "y": 0.5}
          },
          "tiles": []
        }
        """;
        var result = LoadFromJson(json);

        AssertThat(result.TilesetConfig.BaseTileSize).IsEqual(new Vector2I(24, 24));
        AssertThat(result.TilesetConfig.GridOffset.X).IsEqual(0.5f);
        AssertThat(result.TilesetConfig.GridOffset.Y).IsEqual(0.5f);
    }

    /// <summary>
    /// Test partial tilesetConfig uses defaults for missing properties.
    /// </summary>
    [TestCase]
    public void TestTilesetConfig_PartialUsesDefaults()
    {
        var json = """
        {
          "tilesetConfig": {
            "baseTileSize": {"x": 32, "y": 32}
          },
          "tiles": []
        }
        """;
        var result = LoadFromJson(json);

        AssertThat(result.TilesetConfig.BaseTileSize).IsEqual(new Vector2I(32, 32));
        AssertThat(result.TilesetConfig.GridOffset).IsEqual(Vector2.Zero);
    }

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

    // ==================== Helper Methods ====================

    private TileRegistryResult LoadFromJson(string json)
    {
        var filePath = Path.Combine(_testDir, "tiles.json");
        File.WriteAllText(filePath, json);

        // TileDataLoader expects res:// paths, but we can use absolute path
        // by converting the path format
        return TileDataLoader.LoadTileRegistry(filePath);
    }
}
