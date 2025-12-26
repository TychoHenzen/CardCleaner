using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

public class TileRegistry : ITileRegistry
{
    public const int DefaultSourceId = 4;
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

    private readonly Dictionary<string, TileDefinition> _tiles = new();

    public string TilesetPath { get; private set; } = DefaultTilesetPath;

    public TileRegistry()
    {
        LoadFromData();
    }

    public void RegisterTile(TileDefinition tile) => _tiles[tile.Id] = tile;

    public TileDefinition? GetTile(string id) => _tiles.GetValueOrDefault(id);

    public IEnumerable<TileDefinition> GetAllTiles() => _tiles.Values;

    public IEnumerable<TileDefinition> GetTilesByBiome(BiomeType biome)
    {
        foreach (var tile in _tiles.Values)
            if (tile.IsAllowedInBiome(biome))
                yield return tile;
    }

    public void Clear() => _tiles.Clear();

    /// <summary>
    /// Loads tiles from Data/Tiles/tiles.json
    /// </summary>
    public void LoadFromData(string? path = null)
    {
        var result = TileDataLoader.LoadTileRegistry(path);
        TilesetPath = result.TilesetPath;

        foreach (var tile in result.Tiles)
            RegisterTile(tile);

        ILog.Print($"[TileRegistry] Registered {_tiles.Count} tiles from data using tileset {TilesetPath}");
    }

    /// <summary>
    /// Reloads all tiles from the data file, clearing existing tiles first
    /// </summary>
    public void Reload(string? path = null)
    {
        Clear();
        LoadFromData(path);
    }
}
