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
        var floorVisited = _registry.GetTile("floor_visited");

        AssertThat(debugPath).IsNotNull();
        AssertThat(debugTarget).IsNotNull();
        AssertThat(floorVisited).IsNotNull();

        AssertThat(debugPath!.AllowedBiomes).IsNull();
        AssertThat(debugTarget!.AllowedBiomes).IsNull();
        AssertThat(floorVisited!.AllowedBiomes).IsNull();
    }

    [TestCase]
    public void TestTotalTileCount()
    {
        var allTiles = _registry.GetAllTiles().ToList();

        // Should have at least 50 tiles total:
        // 7 universal + 8 per biome * 6 biomes = 55+ tiles
        AssertThat(allTiles.Count).IsGreaterEqual(50);
    }
}
