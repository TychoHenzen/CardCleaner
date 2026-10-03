using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

[TestSuite]
[RequireGodotRuntime]
public class TileRegistryTest
{
    private TileRegistry _registry = null!;

    [BeforeTest]
    public void Setup() => _registry = new TileRegistry();

    [TestCase]
    public void TestTilesAreLoadedFromDataFile()
    {
        var allTiles = _registry.GetAllTiles().ToList();

        // Should have tiles loaded from data file (TSX or JSON)
        AssertThat(allTiles.Count).IsGreater(0);

        // Log what tiles were loaded for debugging
        GD.Print($"Loaded {allTiles.Count} tiles from data file");
        foreach (var tile in allTiles.Take(5))
        {
            GD.Print($"  - {tile.Id}: {tile.Name}");
        }
    }

    [TestCase]
    public void TestTileDefinitionWithSpecificBiomes()
    {
        var tile = new TileDefinition(
            "test_tile",
            "Test Tile",
            TilePassability.Passable,
            new Vector2I(0, 0),
            allowedBiomes: ["forest", "plains"]);

        AssertThat(tile.AllowedBiomes).IsNotNull();
        AssertThat(tile.AllowedBiomes!.Count).IsEqual(2);
        AssertBool(tile.IsAllowedInBiome("forest")).IsTrue();
        AssertBool(tile.IsAllowedInBiome("plains")).IsTrue();
        AssertBool(tile.IsAllowedInBiome("desert")).IsFalse();
        AssertBool(tile.IsAllowedInBiome("tundra")).IsFalse();
    }

    [TestCase]
    public void TestTileDefinitionWithNullBiomesIsUniversal()
    {
        var tile = new TileDefinition(
            "universal_tile",
            "Universal Tile",
            TilePassability.Passable,
            new Vector2I(0, 0));

        AssertThat(tile.AllowedBiomes).IsNull();
        AssertBool(tile.IsAllowedInBiome("plains")).IsTrue();
        AssertBool(tile.IsAllowedInBiome("forest")).IsTrue();
        AssertBool(tile.IsAllowedInBiome("desert")).IsTrue();
        AssertBool(tile.IsAllowedInBiome("tundra")).IsTrue();
        AssertBool(tile.IsAllowedInBiome("swamp")).IsTrue();
        AssertBool(tile.IsAllowedInBiome("mountains")).IsTrue();
    }

    [TestCase]
    public void TestGetTilesByBiomeReturnsCorrectTiles()
    {
        _registry.Clear();

        _registry.RegisterTile(new TileDefinition(
            "forest_only",
            "Forest Only",
            TilePassability.Passable,
            new Vector2I(0, 0),
            allowedBiomes: ["forest"]));

        _registry.RegisterTile(new TileDefinition(
            "desert_only",
            "Desert Only",
            TilePassability.Passable,
            new Vector2I(1, 0),
            allowedBiomes: ["desert"]));

        _registry.RegisterTile(new TileDefinition(
            "universal",
            "Universal",
            TilePassability.Passable,
            new Vector2I(2, 0)));

        var forestTiles = _registry.GetTilesByBiome("forest").ToList();
        var desertTiles = _registry.GetTilesByBiome("desert").ToList();
        var plainsTiles = _registry.GetTilesByBiome("plains").ToList();

        AssertThat(forestTiles.Count).IsEqual(2);
        AssertBool(forestTiles.Any(t => t.Id == "forest_only")).IsTrue();
        AssertBool(forestTiles.Any(t => t.Id == "universal")).IsTrue();
        AssertBool(forestTiles.Any(t => t.Id == "desert_only")).IsFalse();

        AssertThat(desertTiles.Count).IsEqual(2);
        AssertBool(desertTiles.Any(t => t.Id == "desert_only")).IsTrue();
        AssertBool(desertTiles.Any(t => t.Id == "universal")).IsTrue();

        AssertThat(plainsTiles.Count).IsEqual(1);
        AssertBool(plainsTiles.Any(t => t.Id == "universal")).IsTrue();
    }

    // ==================== Data Validation Tests (JSON-specific) ====================
    // These tests validate the JSON tile data file content.
    // They are skipped when loading from TSX during migration.

    [TestCase]
    public void TestBiomeFilteringWithSyntheticData()
    {
        // Test biome filtering works correctly with synthetic data
        // (doesn't depend on production tile counts)
        _registry.Clear();

        _registry.RegisterTile(new TileDefinition(
            "universal1", "Universal 1", TilePassability.Passable, new Vector2I(0, 0)));
        _registry.RegisterTile(new TileDefinition(
            "universal2", "Universal 2", TilePassability.Solid, new Vector2I(1, 0)));
        _registry.RegisterTile(new TileDefinition(
            "forest1", "Forest 1", TilePassability.Passable, new Vector2I(2, 0),
            allowedBiomes: ["forest"]));
        _registry.RegisterTile(new TileDefinition(
            "desert1", "Desert 1", TilePassability.Solid, new Vector2I(3, 0),
            allowedBiomes: ["desert"]));

        var forestTiles = _registry.GetTilesByBiome("forest").ToList();
        var desertTiles = _registry.GetTilesByBiome("desert").ToList();

        // Universal tiles appear in all biomes, plus biome-specific ones
        AssertThat(forestTiles.Count).IsEqual(3); // universal1, universal2, forest1
        AssertThat(desertTiles.Count).IsEqual(3); // universal1, universal2, desert1

        // Restore registry for other tests
        _registry.LoadFromData();
    }

    // ==================== Missing Tile Lookup ====================

    [TestCase]
    public void TestGetTileReturnsNullForMissingId()
    {
        var tile = _registry.GetTile("nonexistent_tile_id_12345");
        AssertThat(tile).IsNull();
    }

    [TestCase]
    public void TestGetTileReturnsNullForEmptyString()
    {
        var tile = _registry.GetTile("");
        AssertThat(tile).IsNull();
    }

    // ==================== Auto-Tile Variant Tests ====================

    [TestCase]
    public void TestCompositableTilesHaveAutoTileVariants()
    {
        // All compositable tiles (outerTerrain="*") must have auto-tile variants
        var compositableTiles = _registry.GetAllTiles()
            .Where(t => t.IsCompositable)
            .ToList();

        var missing = new System.Collections.Generic.List<string>();
        foreach (var tile in compositableTiles)
        {
            if (!tile.HasAutoTileVariants)
                missing.Add(tile.Id);
        }

        if (missing.Count > 0)
            GD.PrintErr($"Compositable tiles without auto-tile variants: {string.Join(", ", missing)}");

        AssertThat(missing.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAutoTileVariantsHaveCorrectCount()
    {
        var tilesWithVariants = _registry.GetAllTiles()
            .Where(t => t.HasAutoTileVariants)
            .ToList();

        var incorrectCounts = new System.Collections.Generic.List<string>();

        foreach (var tile in tilesWithVariants)
        {
            var expected = tile.ExpectedVariantCount;
            var actual = tile.AutoTileVariants!.Length;

            if (actual != expected)
                incorrectCounts.Add($"{tile.Id}: {tile.AutoTileFormatName} expects {expected}, has {actual}");
        }

        if (incorrectCounts.Count > 0)
            GD.PrintErr($"Incorrect variant counts:\n{string.Join("\n", incorrectCounts)}");

        AssertThat(incorrectCounts.Count).IsEqual(0);
    }

    [TestCase]
    public void TestGetAutoTileCoordsReturnsBaseForNullVariant()
    {
        // Create tile with a null variant at index 1
        var variants = new Vector2I?[16];
        variants[0] = null; // Index 0 intentionally null
        variants[15] = new Vector2I(10, 10); // Only variant 15 defined

        var tile = new TileDefinition(
            "test_auto",
            "Test Auto",
            TilePassability.Passable,
            new Vector2I(5, 5),
            autoTileVariants: variants);

        // Index 0 (null) should return base coords
        AssertThat(tile.GetAutoTileCoords(0)).IsEqual(new Vector2I(5, 5));

        // Index 15 (defined) should return variant coords
        AssertThat(tile.GetAutoTileCoords(15)).IsEqual(new Vector2I(10, 10));
    }

    [TestCase]
    public void TestGetAutoTileCoordsReturnsBaseForOutOfBounds()
    {
        var variants = new Vector2I?[16];
        variants[0] = new Vector2I(1, 1);

        var tile = new TileDefinition(
            "test_bounds",
            "Test Bounds",
            TilePassability.Passable,
            new Vector2I(5, 5),
            autoTileVariants: variants);

        // Out of bounds (negative)
        AssertThat(tile.GetAutoTileCoords(-1)).IsEqual(new Vector2I(5, 5));

        // Out of bounds (too large)
        AssertThat(tile.GetAutoTileCoords(100)).IsEqual(new Vector2I(5, 5));
    }

    [TestCase]
    public void TestGetAutoTileCoordsReturnsBaseWhenNoVariants()
    {
        var tile = new TileDefinition(
            "test_no_variants",
            "Test No Variants",
            TilePassability.Passable,
            new Vector2I(7, 8));

        // Any bitmask should return base coords when no variants defined
        AssertThat(tile.GetAutoTileCoords(0)).IsEqual(new Vector2I(7, 8));
        AssertThat(tile.GetAutoTileCoords(15)).IsEqual(new Vector2I(7, 8));
    }

    // ==================== Compiled Atlas Mode Tests ====================

    [TestCase]
    public void TestRegistryReportsCompiledAtlasStatus()
    {
        // Registry should report whether it's using compiled atlas
        // (Will be true if compiled atlas files exist)
        GD.Print($"UsingCompiledAtlas: {_registry.UsingCompiledAtlas}");
        GD.Print($"CompiledTileSet: {(_registry.CompiledTileSet != null ? "loaded" : "null")}");
        GD.Print($"TilesetPath: {_registry.TilesetPath}");

        // No assertion - just log the state for debugging
    }

    [TestCase]
    public void TestAllTilesHaveValidSourceId()
    {
        var allTiles = _registry.GetAllTiles().ToList();
        var invalidSourceIds = new System.Collections.Generic.List<string>();

        foreach (var tile in allTiles)
        {
            // Source ID should be non-negative
            if (tile.SourceId < 0)
                invalidSourceIds.Add($"{tile.Id}: sourceId={tile.SourceId}");
        }

        if (invalidSourceIds.Count > 0)
            GD.PrintErr($"Tiles with invalid source IDs:\n{string.Join("\n", invalidSourceIds)}");

        AssertThat(invalidSourceIds.Count).IsEqual(0);
    }

    [TestCase]
    public void TestAllTilesHaveNonNegativeAtlasCoords()
    {
        var allTiles = _registry.GetAllTiles().ToList();
        var invalidCoords = new System.Collections.Generic.List<string>();

        foreach (var tile in allTiles)
        {
            if (tile.AtlasCoords.X < 0 || tile.AtlasCoords.Y < 0)
                invalidCoords.Add($"{tile.Id}: ({tile.AtlasCoords.X}, {tile.AtlasCoords.Y})");
        }

        if (invalidCoords.Count > 0)
            GD.PrintErr($"Tiles with negative atlas coords:\n{string.Join("\n", invalidCoords)}");

        AssertThat(invalidCoords.Count).IsEqual(0);
    }

    // ==================== Compositable and Fixed Transition Tests ====================

    [TestCase]
    public void TestIsCompositableDetectsWildcardOuterTerrain()
    {
        var tile = new TileDefinition(
            "test_compositable",
            "Test Compositable",
            TilePassability.Passable,
            new Vector2I(0, 0),
            outerTerrainId: "*");

        AssertBool(tile.IsCompositable).IsTrue();
        AssertBool(tile.IsFixedTransition).IsFalse();
    }

    [TestCase]
    public void TestIsFixedTransitionDetectsSpecificTerrain()
    {
        var tile = new TileDefinition(
            "test_fixed",
            "Test Fixed",
            TilePassability.Passable,
            new Vector2I(0, 0),
            outerTerrainId: "dirt");

        AssertBool(tile.IsFixedTransition).IsTrue();
        AssertBool(tile.IsCompositable).IsFalse();
    }

    [TestCase]
    public void TestNullOuterTerrainIsNeitherCompositableNorFixed()
    {
        var tile = new TileDefinition(
            "test_null_outer",
            "Test Null Outer",
            TilePassability.Passable,
            new Vector2I(0, 0),
            outerTerrainId: null);

        AssertBool(tile.IsCompositable).IsFalse();
        AssertBool(tile.IsFixedTransition).IsFalse();
    }

    // ==================== Edge Case Tests ====================

    [TestCase]
    public void TestClearRemovesAllTiles()
    {
        _registry.Clear();
        var allTiles = _registry.GetAllTiles().ToList();

        AssertThat(allTiles.Count).IsEqual(0);

        // Reload to restore state for other tests
        _registry.LoadFromData();
    }

    [TestCase]
    public void TestRegisterTileOverwritesDuplicate()
    {
        _registry.Clear();

        var tile1 = new TileDefinition(
            "dup_test",
            "First Version",
            TilePassability.Passable,
            new Vector2I(0, 0));

        var tile2 = new TileDefinition(
            "dup_test",
            "Second Version",
            TilePassability.Solid,
            new Vector2I(1, 1));

        _registry.RegisterTile(tile1);
        _registry.RegisterTile(tile2);

        var retrieved = _registry.GetTile("dup_test");

        AssertThat(retrieved).IsNotNull();
        AssertThat(retrieved!.Name).IsEqual("Second Version");
        AssertThat(retrieved.Passability).IsEqual(TilePassability.Solid);

        // Reload to restore state
        _registry.LoadFromData();
    }

    // ==================== Dominance Tests ====================

    [TestCase]
    public void TestTileDominanceIsPreserved()
    {
        var tile = new TileDefinition(
            "test_dominance",
            "Test Dominance",
            TilePassability.Passable,
            new Vector2I(0, 0),
            dominance: 42);

        AssertThat(tile.Dominance).IsEqual(42);
    }

    [TestCase]
    public void TestDefaultDominanceIsZero()
    {
        var tile = new TileDefinition(
            "test_default_dom",
            "Test Default Dominance",
            TilePassability.Passable,
            new Vector2I(0, 0));

        AssertThat(tile.Dominance).IsEqual(0);
    }

    // ==================== Auto-Tile Format Tests ====================

    [TestCase]
    public void TestExpectedVariantCountForCorner16()
    {
        var tile = new TileDefinition(
            "test_corner16",
            "Test Corner16",
            TilePassability.Passable,
            new Vector2I(0, 0),
            autoTileFormatName: "corner16");

        AssertThat(tile.ExpectedVariantCount).IsEqual(16);
    }

    [TestCase]
    public void TestExpectedVariantCountForEdge16()
    {
        var tile = new TileDefinition(
            "test_edge16",
            "Test Edge16",
            TilePassability.Passable,
            new Vector2I(0, 0),
            autoTileFormatName: "edge16");

        AssertThat(tile.ExpectedVariantCount).IsEqual(16);
    }

    [TestCase]
    public void TestExpectedVariantCountForBlob47()
    {
        var tile = new TileDefinition(
            "test_blob47",
            "Test Blob47",
            TilePassability.Passable,
            new Vector2I(0, 0),
            autoTileFormatName: "blob47");

        AssertThat(tile.ExpectedVariantCount).IsEqual(47);
    }
}
