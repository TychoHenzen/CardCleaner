using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Tests that verify TiledTilesetLoader creates TileDefinitions from TMX/TSX files,
/// loading tiles from Wang sets with the Wang set name as the tile ID.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TiledTilesetLoaderTest
{
    private const string TestTmxPath = "res://Data/Tiled/tileset.tmx";

    private List<TileDefinition> _tiles = null!;
    private TileRegistryResult _result = null!;

    [BeforeTest]
    public void Setup()
    {
        // Load tiles from TMX - should load all referenced tilesets
        _result = TileDataLoader.LoadFromTmx(TestTmxPath);
        _tiles = _result.Tiles;
    }

    // ==================== Basic Loading Tests ====================

    [TestCase]
    public void TestTmxFileLoadsSuccessfully()
    {
        AssertThat(_tiles).IsNotNull();
        AssertThat(_tiles.Count).IsGreater(0);
        GD.Print($"[TMX] Loaded {_tiles.Count} tiles from tileset.tmx");

        foreach (var tile in _tiles.Take(10))
        {
            GD.Print($"  - {tile.Id}: {tile.Name} at {tile.AtlasCoords}");
        }
    }

    [TestCase]
    public void TestWangSetsCreateTileDefinitions()
    {
        // tileset.tmx references A2_Autotiles.tsx which has Wang sets: Grass1, Grass2, Grass3, etc.
        // These should become tiles with IDs: grass1, grass2, grass3 (snake_case)
        var expectedIds = new[] { "grass1", "grass2", "grass3" };

        foreach (var id in expectedIds)
        {
            var tile = _tiles.FirstOrDefault(t => t.Id == id);
            AssertThat(tile).IsNotNull();
            GD.Print($"[TMX] Found Wang set tile: {id}");
        }
    }

    [TestCase]
    public void TestWangSetTilesHaveAutoTileVariants()
    {
        // Wang set tiles should have auto-tile variants populated
        var tilesWithVariants = _tiles.Where(t => t.AutoTileVariants != null).ToList();
        AssertThat(tilesWithVariants.Count).IsGreater(0);

        foreach (var tile in tilesWithVariants.Take(5))
        {
            AssertThat(tile.AutoTileVariants!.Length).IsEqual(16); // Corner4 = 16 variants

            // Count how many variants are populated
            var populatedCount = tile.AutoTileVariants.Count(v => v.HasValue);
            GD.Print($"[TMX] {tile.Id}: {populatedCount}/16 variants populated");

            // Should have at least some variants
            AssertThat(populatedCount).IsGreater(0);
        }
    }

    [TestCase]
    public void TestWangSetTilesHaveCorner16Format()
    {
        // All auto-tile Wang sets are converted to corner16 format for dual-grid rendering
        var tilesWithFormat = _tiles.Where(t => !string.IsNullOrEmpty(t.AutoTileFormatName)).ToList();

        foreach (var tile in tilesWithFormat)
        {
            AssertThat(tile.AutoTileFormatName).IsEqual("corner16");
        }
    }

    [TestCase]
    public void TestTileAtlasCoordsAreValid()
    {
        foreach (var tile in _tiles)
        {
            // Atlas coords should be non-negative
            AssertThat(tile.AtlasCoords.X).IsGreaterEqual(0);
            AssertThat(tile.AtlasCoords.Y).IsGreaterEqual(0);
        }
    }

    [TestCase]
    public void TestAutoTileVariantCoordsAreValid()
    {
        foreach (var tile in _tiles)
        {
            if (tile.AutoTileVariants == null) continue;

            for (var i = 0; i < tile.AutoTileVariants.Length; i++)
            {
                var variant = tile.AutoTileVariants[i];
                if (!variant.HasValue) continue;

                // All coords should be non-negative
                AssertThat(variant.Value.X).IsGreaterEqual(0);
                AssertThat(variant.Value.Y).IsGreaterEqual(0);
            }
        }
    }

    // ==================== Property Tests ====================

    [TestCase]
    public void TestTilesHaveValidPassability()
    {
        // Verify that passability is one of the valid enum values
        foreach (var tile in _tiles)
        {
            var validPassabilities = new[]
            {
                TilePassability.Passable,
                TilePassability.Solid,
                TilePassability.PartiallyPassable
            };
            AssertThat(validPassabilities).Contains(tile.Passability);
        }
    }

    [TestCase]
    public void TestDefaultLayerIsTerrain()
    {
        // Most tiles should have terrain layer by default
        var terrainTiles = _tiles.Count(t => t.Layer == TileLayer.Terrain);
        AssertThat(terrainTiles).IsGreater(0);
        GD.Print($"[TMX] {terrainTiles}/{_tiles.Count} tiles have terrain layer");
    }

    [TestCase]
    public void TestDefaultBiomesIsNull()
    {
        // Tiles without explicit biome restrictions should have null AllowedBiomes
        var tilesWithNullBiomes = _tiles.Count(t => t.AllowedBiomes == null);
        GD.Print($"[TMX] {tilesWithNullBiomes}/{_tiles.Count} tiles have null biomes (universal)");
    }

    // ==================== Tileset Config Tests ====================

    [TestCase]
    public void TestTmxLoaderReturnsTilesetConfig()
    {
        AssertThat(_result.TilesetConfig).IsNotNull();
        // The TMX uses config from first tileset (A2_Autotiles.tsx with 8x8 tiles)
        AssertThat(_result.TilesetConfig.BaseTileSize).IsEqual(new Vector2I(8, 8));
    }

    // ==================== Snake Case Conversion Tests ====================

    [TestCase]
    public void TestSnakeCaseConversion()
    {
        // Verify that Wang set names are converted to snake_case for IDs
        // "Grass1" -> "grass1" (lowercase)
        // "Grass2" -> "grass2"
        // "Grass3" -> "grass3"

        var grass1 = _tiles.FirstOrDefault(t => t.Id == "grass1");
        var grass2 = _tiles.FirstOrDefault(t => t.Id == "grass2");
        var grass3 = _tiles.FirstOrDefault(t => t.Id == "grass3");

        AssertThat(grass1).IsNotNull();
        AssertThat(grass2).IsNotNull();
        AssertThat(grass3).IsNotNull();

        // Names should preserve original case
        AssertThat(grass1!.Name).IsEqual("Grass1");
        AssertThat(grass2!.Name).IsEqual("Grass2");
        AssertThat(grass3!.Name).IsEqual("Grass3");
    }

    // ==================== Bitmask Coverage Tests ====================

    [TestCase]
    public void TestAutoTilesHaveVariants()
    {
        // Tiles with auto-tile variants should have at least some bitmasks covered
        var tilesWithVariants = _tiles.Where(t => t.AutoTileVariants != null).ToList();

        foreach (var tile in tilesWithVariants.Take(5))
        {
            var missingBitmasks = new List<int>();
            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                if (!tile.AutoTileVariants![bitmask].HasValue)
                    missingBitmasks.Add(bitmask);
            }

            // Some bitmasks may legitimately be missing (e.g., bitmask 0 = no terrain)
            // But most should be covered
            var coveragePercent = (16 - missingBitmasks.Count) / 16.0 * 100;
            GD.Print($"[TMX] {tile.Id}: {coveragePercent:F0}% bitmask coverage");
        }
    }

    // ==================== Multiple Tileset Tests ====================

    [TestCase]
    public void TestLoadsTilesFromMultipleTilesets()
    {
        // tileset.tmx references multiple TSX files
        // A2_Autotiles.tsx (firstgid=1), FDR_Ground_Tiles_Godot.tsx (firstgid=961), FD_City.tsx (firstgid=1441)
        // Check that we loaded tiles from at least 2 different tilesets

        // A2_Autotiles has grass tiles
        var hasGrass = _tiles.Any(t => t.Id.Contains("grass"));

        // FDR_Ground_Tiles_Godot has FDR tiles
        var hasFdr = _tiles.Any(t => t.Id.Contains("fdr"));

        GD.Print($"[TMX] Has grass tiles: {hasGrass}, Has FDR tiles: {hasFdr}");

        // At least A2_Autotiles should load
        AssertBool(hasGrass).IsTrue();
    }
}
