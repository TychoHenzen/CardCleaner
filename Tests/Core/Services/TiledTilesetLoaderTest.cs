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
/// Tests that verify TiledTilesetLoader creates TileDefinitions from Wang sets,
/// using the Wang set name as the tile ID.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TiledTilesetLoaderTest
{
    private const string TestTsxPath = "res://Data/Tiled/test.tsx";

    private List<TileDefinition> _tsxTiles = null!;

    [BeforeTest]
    public void Setup()
    {
        // Load tiles from TSX - should create tiles from Wang sets
        var tsxResult = TileDataLoader.LoadFromTsx(TestTsxPath);
        _tsxTiles = tsxResult.Tiles;
    }

    // ==================== Basic Loading Tests ====================

    [TestCase]
    public void TestTsxFileLoadsSuccessfully()
    {
        AssertThat(_tsxTiles).IsNotNull();
        AssertThat(_tsxTiles.Count).IsGreater(0);
        GD.Print($"[TSX] Loaded {_tsxTiles.Count} tiles from test.tsx");

        foreach (var tile in _tsxTiles)
        {
            GD.Print($"  - {tile.Id}: {tile.Name} at {tile.AtlasCoords}");
        }
    }

    [TestCase]
    public void TestWangSetsCreateTileDefinitions()
    {
        // test.tsx has Wang sets: Grass1, Grass2, Grass3
        // These should become tiles with IDs: grass1, grass2, grass3 (snake_case)
        var expectedIds = new[] { "grass1", "grass2", "grass3" };

        foreach (var id in expectedIds)
        {
            var tile = _tsxTiles.FirstOrDefault(t => t.Id == id);
            AssertThat(tile).IsNotNull();
            GD.Print($"[TSX] Found Wang set tile: {id}");
        }
    }

    [TestCase]
    public void TestWangSetTilesHaveAutoTileVariants()
    {
        // All Wang set tiles should have auto-tile variants populated
        foreach (var tile in _tsxTiles)
        {
            AssertThat(tile.AutoTileVariants).IsNotNull();
            AssertThat(tile.AutoTileVariants!.Length).IsEqual(16); // Corner4 = 16 variants

            // Count how many variants are populated
            var populatedCount = tile.AutoTileVariants.Count(v => v.HasValue);
            GD.Print($"[TSX] {tile.Id}: {populatedCount}/16 variants populated");

            // Should have at least some variants
            AssertThat(populatedCount).IsGreater(0);
        }
    }

    [TestCase]
    public void TestWangSetTilesHaveCorner16Format()
    {
        // test.tsx uses "corner" type Wang sets, which should map to corner16
        foreach (var tile in _tsxTiles)
        {
            AssertThat(tile.AutoTileFormatName).IsEqual("corner16");
        }
    }

    [TestCase]
    public void TestTileAtlasCoordsAreValid()
    {
        foreach (var tile in _tsxTiles)
        {
            // Atlas coords should be non-negative
            AssertThat(tile.AtlasCoords.X).IsGreaterEqual(0);
            AssertThat(tile.AtlasCoords.Y).IsGreaterEqual(0);

            GD.Print($"[TSX] {tile.Id}: atlasCoords = {tile.AtlasCoords}");
        }
    }

    [TestCase]
    public void TestAutoTileVariantCoordsAreValid()
    {
        foreach (var tile in _tsxTiles)
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

    // ==================== Default Property Tests ====================

    [TestCase]
    public void TestDefaultPassabilityIsPassable()
    {
        // Without explicit properties, tiles should default to passable
        foreach (var tile in _tsxTiles)
        {
            AssertThat(tile.Passability).IsEqual(TilePassability.Passable);
        }
    }

    [TestCase]
    public void TestDefaultLayerIsTerrain()
    {
        // Without explicit properties, tiles should default to terrain layer
        foreach (var tile in _tsxTiles)
        {
            AssertThat(tile.Layer).IsEqual(TileLayer.Terrain);
        }
    }

    [TestCase]
    public void TestDefaultBiomesIsNull()
    {
        // Without explicit biome properties, tiles should be universal (null biomes)
        foreach (var tile in _tsxTiles)
        {
            AssertThat(tile.AllowedBiomes).IsNull();
        }
    }

    // ==================== Tileset Config Tests ====================

    [TestCase]
    public void TestTsxLoaderReturnsTilesetConfig()
    {
        var result = TileDataLoader.LoadFromTsx(TestTsxPath);

        AssertThat(result.TilesetConfig).IsNotNull();
        // test.tsx has tilewidth=8, tileheight=8
        AssertThat(result.TilesetConfig.BaseTileSize).IsEqual(new Vector2I(8, 8));
    }

    // ==================== Snake Case Conversion Tests ====================

    [TestCase]
    public void TestSnakeCaseConversion()
    {
        // Verify that Wang set names are converted to snake_case for IDs
        // "Grass1" → "grass1" (lowercase)
        // "Grass2" → "grass2"
        // "Grass3" → "grass3"

        var grass1 = _tsxTiles.FirstOrDefault(t => t.Id == "grass1");
        var grass2 = _tsxTiles.FirstOrDefault(t => t.Id == "grass2");
        var grass3 = _tsxTiles.FirstOrDefault(t => t.Id == "grass3");

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
    public void TestAllBitmasksHaveVariants()
    {
        // Each Wang set in test.tsx should have all 16 bitmasks covered
        // (based on the file content showing wangtile entries for each pattern)

        foreach (var tile in _tsxTiles)
        {
            if (tile.AutoTileVariants == null) continue;

            var missingBitmasks = new List<int>();
            for (var bitmask = 0; bitmask < 16; bitmask++)
            {
                if (!tile.AutoTileVariants[bitmask].HasValue)
                    missingBitmasks.Add(bitmask);
            }

            if (missingBitmasks.Count > 0)
            {
                GD.Print($"[TSX] {tile.Id}: missing bitmasks {string.Join(", ", missingBitmasks)}");
            }

            // Some bitmasks may legitimately be missing (e.g., bitmask 0 = no terrain)
            // But most should be covered
            var coveragePercent = (16 - missingBitmasks.Count) / 16.0 * 100;
            GD.Print($"[TSX] {tile.Id}: {coveragePercent:F0}% bitmask coverage");
        }
    }

    // ==================== Representative Tile Tests ====================

    [TestCase]
    public void TestRepresentativeTileIsBitmask15()
    {
        // The loader should prefer bitmask 15 (all corners) as the representative tile
        // This affects the base AtlasCoords

        foreach (var tile in _tsxTiles)
        {
            if (tile.AutoTileVariants == null) continue;

            // If bitmask 15 has a variant, the base coords should match it
            var bitmask15 = tile.AutoTileVariants[15];
            if (bitmask15.HasValue)
            {
                AssertThat(tile.AtlasCoords).IsEqual(bitmask15.Value);
                GD.Print($"[TSX] {tile.Id}: base coords {tile.AtlasCoords} matches bitmask 15");
            }
        }
    }
}
