using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
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
    public void TestDefaultTilesAreRegistered()
    {
        var allTiles = _registry.GetAllTiles().ToList();

        // Should have tiles loaded from data file
        AssertThat(allTiles.Count).IsGreater(0);
        // Universal tiles should exist
        AssertThat(_registry.GetTile("dirt")).IsNotNull();
        AssertThat(_registry.GetTile("wall")).IsNotNull();
        AssertThat(_registry.GetTile("stone")).IsNotNull();
    }

    [TestCase]
    public void TestTileDefinitionWithSpecificBiomes()
    {
        var tile = new TileDefinition(
            "test_tile",
            "Test Tile",
            TilePassability.Passable,
            new Vector2I(0, 0),
            allowedBiomes: [BiomeType.Forest, BiomeType.Plains]);

        AssertThat(tile.AllowedBiomes).IsNotNull();
        AssertThat(tile.AllowedBiomes!.Count).IsEqual(2);
        AssertBool(tile.IsAllowedInBiome(BiomeType.Forest)).IsTrue();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Plains)).IsTrue();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Desert)).IsFalse();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Tundra)).IsFalse();
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
        AssertBool(tile.IsAllowedInBiome(BiomeType.Plains)).IsTrue();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Forest)).IsTrue();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Desert)).IsTrue();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Tundra)).IsTrue();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Swamp)).IsTrue();
        AssertBool(tile.IsAllowedInBiome(BiomeType.Mountains)).IsTrue();
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
            allowedBiomes: [BiomeType.Forest]));

        _registry.RegisterTile(new TileDefinition(
            "desert_only",
            "Desert Only",
            TilePassability.Passable,
            new Vector2I(1, 0),
            allowedBiomes: [BiomeType.Desert]));

        _registry.RegisterTile(new TileDefinition(
            "universal",
            "Universal",
            TilePassability.Passable,
            new Vector2I(2, 0)));

        var forestTiles = _registry.GetTilesByBiome(BiomeType.Forest).ToList();
        var desertTiles = _registry.GetTilesByBiome(BiomeType.Desert).ToList();
        var plainsTiles = _registry.GetTilesByBiome(BiomeType.Plains).ToList();

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

    [TestCase]
    public void TestEachBiomeHasTiles()
    {
        // Each biome should have specific tiles
        var plainsTiles = _registry.GetTilesByBiome(BiomeType.Plains).ToList();
        var forestTiles = _registry.GetTilesByBiome(BiomeType.Forest).ToList();
        var desertTiles = _registry.GetTilesByBiome(BiomeType.Desert).ToList();
        var tundraTiles = _registry.GetTilesByBiome(BiomeType.Tundra).ToList();
        var swampTiles = _registry.GetTilesByBiome(BiomeType.Swamp).ToList();
        var mountainsTiles = _registry.GetTilesByBiome(BiomeType.Mountains).ToList();

        // Each biome should have multiple tiles (passable + blocked)
        AssertThat(plainsTiles.Count).IsGreater(5);
        AssertThat(forestTiles.Count).IsGreater(5);
        AssertThat(desertTiles.Count).IsGreater(5);
        AssertThat(tundraTiles.Count).IsGreater(5);
        AssertThat(swampTiles.Count).IsGreater(5);
        AssertThat(mountainsTiles.Count).IsGreater(5);

        // Each biome should have at least one passable and one solid tile
        AssertBool(plainsTiles.Any(t => t.IsPassable)).IsTrue();
        AssertBool(plainsTiles.Any(t => !t.IsPassable)).IsTrue();
        AssertBool(forestTiles.Any(t => t.IsPassable)).IsTrue();
        AssertBool(forestTiles.Any(t => !t.IsPassable)).IsTrue();
        AssertBool(desertTiles.Any(t => t.IsPassable)).IsTrue();
        AssertBool(desertTiles.Any(t => !t.IsPassable)).IsTrue();
        AssertBool(tundraTiles.Any(t => t.IsPassable)).IsTrue();
        AssertBool(tundraTiles.Any(t => !t.IsPassable)).IsTrue();
        AssertBool(swampTiles.Any(t => t.IsPassable)).IsTrue();
        AssertBool(swampTiles.Any(t => !t.IsPassable)).IsTrue();
        AssertBool(mountainsTiles.Any(t => t.IsPassable)).IsTrue();
        AssertBool(mountainsTiles.Any(t => !t.IsPassable)).IsTrue();
    }

    [TestCase]
    public void TestBiomeTilesHaveDistinctPrefixes()
    {
        // Biome-specific tiles should be named with biome prefix
        var plainsTiles = _registry.GetTilesByBiome(BiomeType.Plains)
            .Where(t => t.AllowedBiomes?.Contains(BiomeType.Plains) == true)
            .ToList();
        var forestTiles = _registry.GetTilesByBiome(BiomeType.Forest)
            .Where(t => t.AllowedBiomes?.Contains(BiomeType.Forest) == true)
            .ToList();

        // Plains-specific tiles should start with "plains_"
        AssertBool(plainsTiles.All(t => t.Id.StartsWith("plains_"))).IsTrue();
        // Forest-specific tiles should start with "forest_"
        AssertBool(forestTiles.All(t => t.Id.StartsWith("forest_"))).IsTrue();
    }

    [TestCase]
    public void TestDirtTileIsUniversal()
    {
        var dirt = _registry.GetTile("dirt");

        AssertThat(dirt).IsNotNull();
        AssertThat(dirt!.AllowedBiomes).IsNull();
        AssertBool(dirt.IsAllowedInBiome(BiomeType.Plains)).IsTrue();
        AssertBool(dirt.IsAllowedInBiome(BiomeType.Forest)).IsTrue();
        AssertBool(dirt.IsAllowedInBiome(BiomeType.Desert)).IsTrue();
        AssertBool(dirt.IsAllowedInBiome(BiomeType.Tundra)).IsTrue();
    }

    [TestCase]
    public void TestWallTileIsUniversal()
    {
        var wall = _registry.GetTile("wall");

        AssertThat(wall).IsNotNull();
        AssertThat(wall!.AllowedBiomes).IsNull();
        AssertBool(wall.IsAllowedInBiome(BiomeType.Plains)).IsTrue();
        AssertBool(wall.IsAllowedInBiome(BiomeType.Forest)).IsTrue();
    }

    [TestCase]
    public void TestDebugTilesAreUniversal()
    {
        var debugPath = _registry.GetTile("debug_path");
        var debugTarget = _registry.GetTile("debug_target");

        AssertThat(debugPath).IsNotNull();
        AssertThat(debugTarget).IsNotNull();

        AssertThat(debugPath!.AllowedBiomes).IsNull();
        AssertThat(debugTarget!.AllowedBiomes).IsNull();
    }

    [TestCase]
    public void TestTotalTileCount()
    {
        var allTiles = _registry.GetAllTiles().ToList();

        // Should have at least 50 tiles total:
        // 7 universal + 8 per biome * 6 biomes = 55+ tiles
        AssertThat(allTiles.Count).IsGreaterEqual(50);
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
                incorrectCounts.Add($"{tile.Id}: {tile.AutoTileFormat} expects {expected}, has {actual}");
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
            autoTileFormat: AutoTileFormat.Corner16);

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
            autoTileFormat: AutoTileFormat.Edge16);

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
            autoTileFormat: AutoTileFormat.Blob47);

        AssertThat(tile.ExpectedVariantCount).IsEqual(47);
    }
}
