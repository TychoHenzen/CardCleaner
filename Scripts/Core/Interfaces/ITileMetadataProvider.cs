using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Provides tile classification and query methods for dynamic tile selection.
/// This interface abstracts away hardcoded tile names, allowing the map generator
/// and WFC algorithm to work with any tile set defined in TSX/JSON.
/// </summary>
public interface ITileMetadataProvider
{
    /// <summary>
    /// Gets all simple terrain tiles (terrain layer, no auto-tile variants).
    /// These can serve as base backgrounds for compositable auto-tiles.
    /// </summary>
    IReadOnlyList<TileDefinition> GetSimpleTerrainTiles();

    /// <summary>
    /// Gets all auto-tiles (tiles with auto-tile variants).
    /// </summary>
    IReadOnlyList<TileDefinition> GetAutoTiles();

    /// <summary>
    /// Gets all gap tiles (tiles marked as gap fillers between auto-tiles).
    /// </summary>
    IReadOnlyList<TileDefinition> GetGapTiles();

    /// <summary>
    /// Gets all passable terrain tiles (terrain layer, passability != Solid).
    /// </summary>
    IReadOnlyList<TileDefinition> GetPassableTerrainTiles();

    /// <summary>
    /// Gets all solid terrain tiles (terrain layer, passability == Solid).
    /// </summary>
    IReadOnlyList<TileDefinition> GetSolidTerrainTiles();

    /// <summary>
    /// Gets all decoration tiles (decoration layer).
    /// </summary>
    IReadOnlyList<TileDefinition> GetDecorationTiles();

    /// <summary>
    /// Gets tiles filtered by layer.
    /// </summary>
    IReadOnlyList<TileDefinition> GetTilesByLayer(TileLayer layer);

    /// <summary>
    /// Gets tiles allowed in the specified biome.
    /// </summary>
    IReadOnlyList<TileDefinition> GetTilesByBiome(string biomeId);

    /// <summary>
    /// Gets tiles that can be used as background for compositable auto-tiles.
    /// Returns simple terrain tiles that are passable (or optionally all simple terrain).
    /// </summary>
    IReadOnlyList<TileDefinition> GetBackgroundTerrainTiles();

    /// <summary>
    /// Gets a default passable tile for fallback scenarios.
    /// Returns the first passable terrain tile, or null if none available.
    /// </summary>
    TileDefinition? GetDefaultPassableTile();

    /// <summary>
    /// Gets a default solid tile for fallback scenarios (e.g., out-of-bounds).
    /// Returns the first solid terrain tile, or null if none available.
    /// </summary>
    TileDefinition? GetDefaultSolidTile();

    /// <summary>
    /// Gets a default gap tile for filling gaps between auto-tiles.
    /// Returns the first gap tile, or falls back to a simple passable terrain tile.
    /// </summary>
    TileDefinition? GetDefaultGapTile();

    /// <summary>
    /// Checks if the tile with the given ID is an auto-tile.
    /// </summary>
    bool IsAutoTile(string tileId);

    /// <summary>
    /// Checks if the tile with the given ID is passable.
    /// </summary>
    bool IsPassable(string tileId);

    /// <summary>
    /// Checks if the tile with the given ID is solid.
    /// </summary>
    bool IsSolid(string tileId);

    /// <summary>
    /// Checks if the tile with the given ID is a gap tile.
    /// </summary>
    bool IsGapTile(string tileId);

    /// <summary>
    /// Checks if the tile with the given ID is transparent (for line-of-sight).
    /// </summary>
    bool IsTransparent(string tileId);

    /// <summary>
    /// Gets all tile IDs that can be used for WFC generation.
    /// This typically includes all terrain-layer tiles.
    /// </summary>
    IReadOnlyList<string> GetWfcTileIds();

    /// <summary>
    /// Gets the tile ID for the default passable tile.
    /// </summary>
    string? GetDefaultPassableTileId();

    /// <summary>
    /// Gets the tile ID for the default solid tile.
    /// </summary>
    string? GetDefaultSolidTileId();

    /// <summary>
    /// Gets the tile ID for the default gap tile.
    /// </summary>
    string? GetDefaultGapTileId();
}
