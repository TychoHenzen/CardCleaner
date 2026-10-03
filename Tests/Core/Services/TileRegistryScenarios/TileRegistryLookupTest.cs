using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Core.Services.TileRegistryScenarios;

/// <summary>
///     TileRegistry loading, biome filtering and missing tile lookup scenarios split out of TileRegistryTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TileRegistryLookupTest : TileRegistryTestBase
{
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
            new TileDefinitionOptions
            {
                AllowedBiomes = ["forest", "plains"]
            });

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
            new TileDefinitionOptions
            {
                AllowedBiomes = ["forest"]
            }));

        _registry.RegisterTile(new TileDefinition(
            "desert_only",
            "Desert Only",
            TilePassability.Passable,
            new Vector2I(1, 0),
            new TileDefinitionOptions
            {
                AllowedBiomes = ["desert"]
            }));

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
            "universal1",
            "Universal 1",
            TilePassability.Passable,
            new Vector2I(0, 0)));
        _registry.RegisterTile(new TileDefinition(
            "universal2",
            "Universal 2",
            TilePassability.Solid,
            new Vector2I(1, 0)));
        _registry.RegisterTile(new TileDefinition(
            "forest1",
            "Forest 1",
            TilePassability.Passable,
            new Vector2I(2, 0),
            new TileDefinitionOptions
            {
                AllowedBiomes = ["forest"]
            }));
        _registry.RegisterTile(new TileDefinition(
            "desert1",
            "Desert 1",
            TilePassability.Solid,
            new Vector2I(3, 0),
            new TileDefinitionOptions
            {
                AllowedBiomes = ["desert"]
            }));

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
}
