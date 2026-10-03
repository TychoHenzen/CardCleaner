using Godot;

namespace CardCleaner.Tests.Features.Worldgen.AutoTiling.TileDataLoaderFormat;

/// <summary>
///     TileDataLoaderTilesetConfigTest scenarios split out of TileDataLoaderFormatTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileDataLoaderTilesetConfigTest : TileDataLoaderFormatTestBase
{
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
}
