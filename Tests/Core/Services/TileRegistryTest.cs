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

        AssertThat(allTiles.Count).IsGreater(0);
        AssertThat(_registry.GetTile("floor")).IsNotNull();
        AssertThat(_registry.GetTile("grass")).IsNotNull();
        AssertThat(_registry.GetTile("dirt")).IsNotNull();
        AssertThat(_registry.GetTile("wall")).IsNotNull();
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
    public void TestGrassTileAllowedInPlainsAndForest()
    {
        var grass = _registry.GetTile("grass");

        AssertThat(grass).IsNotNull();
        AssertBool(grass!.IsAllowedInBiome(BiomeType.Plains)).IsTrue();
        AssertBool(grass.IsAllowedInBiome(BiomeType.Forest)).IsTrue();
        AssertBool(grass.IsAllowedInBiome(BiomeType.Desert)).IsFalse();
        AssertBool(grass.IsAllowedInBiome(BiomeType.Tundra)).IsFalse();
    }

    [TestCase]
    public void TestFloorTileAllowedInDesertAndTundra()
    {
        var floor = _registry.GetTile("floor");

        AssertThat(floor).IsNotNull();
        AssertBool(floor!.IsAllowedInBiome(BiomeType.Desert)).IsTrue();
        AssertBool(floor.IsAllowedInBiome(BiomeType.Tundra)).IsTrue();
        AssertBool(floor.IsAllowedInBiome(BiomeType.Plains)).IsFalse();
        AssertBool(floor.IsAllowedInBiome(BiomeType.Forest)).IsFalse();
    }

    [TestCase]
    public void TestWaterTileOnlyAllowedInTundra()
    {
        var water = _registry.GetTile("water");

        AssertThat(water).IsNotNull();
        AssertBool(water!.IsAllowedInBiome(BiomeType.Tundra)).IsTrue();
        AssertBool(water.IsAllowedInBiome(BiomeType.Plains)).IsFalse();
        AssertBool(water.IsAllowedInBiome(BiomeType.Forest)).IsFalse();
        AssertBool(water.IsAllowedInBiome(BiomeType.Desert)).IsFalse();
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
}
