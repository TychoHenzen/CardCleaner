using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// The tile data the WFC code reads, without the registry that owns it. Unknown tile ids get the neutral answer
/// documented on each member, so a caller that has not checked <see cref="Contains"/> still gets a safe result.
/// </summary>
public interface IWfcTileCatalog
{
    /// <summary>Every tile id, in the order the registry enumerates them. Read lazily on each enumeration.</summary>
    IEnumerable<string> TileIds { get; }

    /// <summary>Whether a tile with this id is in the catalog.</summary>
    bool Contains(string tileId);

    /// <summary>Whether the tile has auto-tile variants. False for an unknown id.</summary>
    bool IsAutoTile(string tileId);

    /// <summary>
    /// Whether an auto-tile has a variant for the solid-fill bitmask (15).
    /// False for an unknown id and for a tile that is not an auto-tile.
    /// </summary>
    bool HasSolidFillVariant(string tileId);

    /// <summary>Whether the tile is passable. False for an unknown id.</summary>
    bool IsPassable(string tileId);

    /// <summary>The tile's selection probability. 1 for an unknown id, so check Contains first.</summary>
    float GetProbability(string tileId);

    /// <summary>Whether the tile lists the biomes it may appear in. False for an unknown id.</summary>
    bool HasBiomeRestriction(string tileId);

    /// <summary>Whether the tile may appear in the biome. False for an unknown id.</summary>
    bool IsAllowedInBiome(string tileId, string biomeId);

    /// <summary>Whether two tile ids name the same terrain type, by the registry's rule.</summary>
    bool AreSameTerrainType(string? tileId1, string? tileId2);

    /// <summary>The variation group of the tile, or null when the tile belongs to none.</summary>
    WfcTileVariation? GetVariation(string tileId);

    /// <summary>
    /// Whether the tile's auto-tile format allows the bitmask.
    /// Null when the tile is unknown or has no auto-tile format.
    /// </summary>
    bool? IsBitmaskAllowed(string tileId, int bitmask);

    /// <summary>
    /// The largest multi-cell footprint of the tile's auto-tile format, with its offset.
    /// Null when the tile is unknown, has no format, or has no multi-cell variant.
    /// </summary>
    (Vector2I Size, Vector2I Offset)? GetMultiCellBounds(string tileId);
}
