using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Read-only classification and default-tile queries over the registered tile definitions.
/// </summary>
internal sealed class TileCatalogQueries
{
    private readonly IReadOnlyDictionary<string, TileDefinition> _tiles;

    internal TileCatalogQueries(IReadOnlyDictionary<string, TileDefinition> tiles)
    {
        _tiles = tiles;
    }

    internal IReadOnlyList<TileDefinition> GetSimpleTerrainTiles() => Where(t => t.IsSimpleTerrain);

    internal IReadOnlyList<TileDefinition> GetAutoTiles() => Where(t => t.IsAutoTile);

    internal IReadOnlyList<TileDefinition> GetGapTiles() => Where(t => t.IsGapTile);

    internal IReadOnlyList<TileDefinition> GetPassableTerrainTiles()
        => Where(t => t.Layer == TileLayer.Terrain && t.IsPassable);

    internal IReadOnlyList<TileDefinition> GetSolidTerrainTiles()
        => Where(t => t.Layer == TileLayer.Terrain && t.IsSolid);

    internal IReadOnlyList<TileDefinition> GetDecorationTiles() => Where(t => t.IsDecoration);

    internal IReadOnlyList<TileDefinition> GetTilesByLayer(TileLayer layer) => Where(t => t.Layer == layer);

    internal IReadOnlyList<TileDefinition> GetTilesByBiome(string biomeId) => Where(t => t.IsAllowedInBiome(biomeId));

    // Simple terrain tiles that can serve as backgrounds for auto-tiles
    internal IReadOnlyList<TileDefinition> GetBackgroundTerrainTiles()
        => Where(t => t.IsSimpleTerrain && t.IsPassable);

    internal TileDefinition? GetDefaultPassableTile()
    {
        // First try gap tiles (they're specifically meant to be passable fillers)
        var gapTile = _tiles.Values.FirstOrDefault(t => t.IsGapTile && t.IsPassable);
        if (gapTile != null) return gapTile;

        // Then try simple passable terrain
        return _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable)
            ?? _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsPassable);
    }

    internal TileDefinition? GetDefaultSolidTile()
    {
        return _tiles.Values.FirstOrDefault(t => t.Layer == TileLayer.Terrain && t.IsSolid);
    }

    internal TileDefinition? GetDefaultGapTile()
    {
        // First try explicit gap tiles
        var gapTile = _tiles.Values.FirstOrDefault(t => t.IsGapTile);
        if (gapTile != null) return gapTile;

        // Fall back to any simple passable terrain tile
        return _tiles.Values.FirstOrDefault(t => t.IsSimpleTerrain && t.IsPassable);
    }

    internal bool IsAutoTile(string tileId) => _tiles.TryGetValue(tileId, out var tile) && tile.IsAutoTile;

    internal bool IsPassable(string tileId) => _tiles.TryGetValue(tileId, out var tile) && tile.IsPassable;

    internal bool IsSolid(string tileId) => _tiles.TryGetValue(tileId, out var tile) && tile.IsSolid;

    internal bool IsGapTile(string tileId) => _tiles.TryGetValue(tileId, out var tile) && tile.IsGapTile;

    internal bool IsTransparent(string tileId) => _tiles.TryGetValue(tileId, out var tile) && tile.IsTransparent;

    internal IReadOnlyList<string> GetWfcTileIds()
    {
        return _tiles.Values
            .Where(t => t.Layer == TileLayer.Terrain)
            .Select(t => t.Id)
            .ToList();
    }

    private List<TileDefinition> Where(System.Func<TileDefinition, bool> predicate)
        => _tiles.Values.Where(predicate).ToList();
}
