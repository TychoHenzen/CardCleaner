using System;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Helper class for applying per-tile auto-tiling using both 4-bit corner and 8-bit blob formats.
/// </summary>
public static class AutoTileHelper
{
    /// <summary>
    ///     Compute the appropriate bitmask for a tile based on its format.
    /// </summary>
    /// <param name="position">The tile position</param>
    /// <param name="tileDef">The tile definition (determines format)</param>
    /// <param name="getTileId">Function to get tile ID at a position</param>
    /// <returns>The bitmask value appropriate for the tile's format</returns>
    public static int ComputeBitmask(Vector2I position, TileDefinition tileDef, Func<Vector2I, string?> getTileId)
    {
        Func<Vector2I, bool> isSameTerrain = neighborPos =>
        {
            var neighborId = getTileId(neighborPos);
            return neighborId == tileDef.Id;
        };

        var format = tileDef.GetAutoTileFormat();
        var bitmaskType = format?.BitmaskType ?? BitmaskType.Corner4;

        return bitmaskType switch
        {
            BitmaskType.Full8 => NeighborBitmask8.Compute(position, isSameTerrain),
            BitmaskType.Edge4 => NeighborBitmask.Compute(position, isSameTerrain),
            _ => NeighborBitmaskCorner.Compute(position, isSameTerrain)
        };
    }

    /// <summary>
    ///     Get the variant definition for a tile at a specific position.
    /// </summary>
    /// <param name="position">The tile position</param>
    /// <param name="tileDef">The tile definition</param>
    /// <param name="getTileId">Function to get tile ID at a position</param>
    /// <returns>The variant definition, or null if not found</returns>
    public static VariantDefinition? GetAutoTileVariant(
        Vector2I position,
        TileDefinition tileDef,
        Func<Vector2I, string?> getTileId)
    {
        if (!tileDef.HasAutoTileVariants)
            return null;

        var bitmask = ComputeBitmask(position, tileDef, getTileId);
        return TileAutoTileFormats.GetVariantDefinition(tileDef, bitmask);
    }

    /// <summary>
    ///     Get the auto-tile atlas coordinates for a tile at a specific position.
    /// </summary>
    /// <param name="position">The tile position</param>
    /// <param name="tileDef">The tile definition</param>
    /// <param name="getTileId">Function to get tile ID at a position</param>
    /// <returns>The atlas coordinates to use for rendering</returns>
    public static Vector2I GetAutoTileCoords(
        Vector2I position,
        TileDefinition tileDef,
        Func<Vector2I, string?> getTileId)
    {
        if (!tileDef.HasAutoTileVariants)
            return tileDef.AtlasCoords;

        var bitmask = ComputeBitmask(position, tileDef, getTileId);
        return tileDef.GetAutoTileCoords(bitmask);
    }
}
