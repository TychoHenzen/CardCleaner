using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Tests.Mocks;

/// <summary>
/// Mock ITileRegistry for testing. Allows configuring tiles with specific properties
/// without loading from tiles.json.
/// </summary>
public class MockTileRegistry : ITileRegistry
{
    private readonly Dictionary<string, TileDefinition> _tiles = new();

    public string TilesetPath => "res://test.tres";
    public bool UsingCompiledAtlas => false;
    public TileSet? CompiledTileSet => null;
    public TilesetConfig TilesetConfig => TilesetConfig.Default;

    public void RegisterTile(TileDefinition tile) => _tiles[tile.Id] = tile;

    public TileDefinition? GetTile(string id) => _tiles.GetValueOrDefault(id);

    public IEnumerable<TileDefinition> GetAllTiles() => _tiles.Values;

    public IEnumerable<TileDefinition> GetTilesByBiome(string biomeId)
    {
        foreach (var tile in _tiles.Values)
            if (tile.IsAllowedInBiome(biomeId))
                yield return tile;
    }

    public void Clear() => _tiles.Clear();

    /// <summary>
    /// Helper to create a simple auto-tile (HasAutoTileVariants = true).
    /// </summary>
    public static TileDefinition CreateAutoTile(string id, string name = "")
    {
        // Non-null autoTileVariants array makes HasAutoTileVariants return true
        var variants = new Vector2I?[16];
        return new TileDefinition(
            id: id,
            name: string.IsNullOrEmpty(name) ? id : name,
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero,
            autoTileVariants: variants);
    }

    /// <summary>
    /// Helper to create a simple gap/fill tile (HasAutoTileVariants = false).
    /// </summary>
    public static TileDefinition CreateGapTile(string id, string name = "")
    {
        // No autoTileVariants = gap tile
        return new TileDefinition(
            id: id,
            name: string.IsNullOrEmpty(name) ? id : name,
            passability: TilePassability.Passable,
            atlasCoords: Vector2I.Zero);
    }

    /// <summary>
    /// Creates a registry pre-configured with standard test tiles:
    /// - grass, stone = auto-tiles
    /// - dirt, rock = gap tiles
    /// </summary>
    public static MockTileRegistry CreateWithTestTiles()
    {
        var registry = new MockTileRegistry();
        registry.RegisterTile(CreateAutoTile("grass", "Grass"));
        registry.RegisterTile(CreateAutoTile("stone", "Stone"));
        registry.RegisterTile(CreateGapTile("dirt", "Dirt"));
        registry.RegisterTile(CreateGapTile("rock", "Rock"));
        return registry;
    }
}
