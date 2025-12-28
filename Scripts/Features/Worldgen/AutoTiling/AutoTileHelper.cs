using System;
using CardCleaner.Scripts.Core.Interfaces;
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

        return tileDef.AutoTileFormat switch
        {
            AutoTileFormat.Blob47 => NeighborBitmask8.Compute(position, isSameTerrain),
            AutoTileFormat.Edge16 => NeighborBitmask.Compute(position, isSameTerrain),
            _ => NeighborBitmaskCorner.Compute(position, isSameTerrain)
        };
    }

    /// <summary>
    ///     Get the auto-tile atlas coordinates for a tile at a specific position.
    /// </summary>
    /// <param name="position">The tile position</param>
    /// <param name="tileDef">The tile definition</param>
    /// <param name="getTileId">Function to get tile ID at a position</param>
    /// <returns>The atlas coordinates to use for rendering</returns>
    public static Vector2I GetAutoTileCoords(Vector2I position, TileDefinition tileDef, Func<Vector2I, string?> getTileId)
    {
        if (!tileDef.HasAutoTileVariants)
            return tileDef.AtlasCoords;

        var bitmask = ComputeBitmask(position, tileDef, getTileId);
        return tileDef.GetAutoTileCoords(bitmask);
    }

    /// <summary>
    ///     Apply auto-tiling to a TileMapLayer for tiles with per-tile variants.
    /// </summary>
    /// <param name="tileMap">The TileMapLayer to modify</param>
    /// <param name="tileRegistry">Registry to look up tile definitions</param>
    /// <param name="tileIds">2D array of tile IDs [y, x]</param>
    /// <param name="mapSize">Size of the map</param>
    /// <param name="sourceId">The TileSet source ID to use</param>
    /// <returns>Number of tiles that had auto-tile variants applied</returns>
    public static int ApplyToTileMap(TileMapLayer tileMap, ITileRegistry tileRegistry, string[,] tileIds, Vector2I mapSize, int sourceId = 4)
    {
        var variantsApplied = 0;

        Func<Vector2I, string?> getTileId = pos =>
        {
            if (pos.X < 0 || pos.X >= mapSize.X || pos.Y < 0 || pos.Y >= mapSize.Y)
                return null;
            return tileIds[pos.Y, pos.X];
        };

        for (var y = 0; y < mapSize.Y; y++)
        {
            for (var x = 0; x < mapSize.X; x++)
            {
                var position = new Vector2I(x, y);
                var tileId = tileIds[y, x];
                if (string.IsNullOrEmpty(tileId))
                    continue;

                var tileDef = tileRegistry.GetTile(tileId);
                if (tileDef == null || !tileDef.HasAutoTileVariants)
                    continue;

                var coords = GetAutoTileCoords(position, tileDef, getTileId);
                tileMap.SetCell(position, sourceId, coords);
                variantsApplied++;
            }
        }

        return variantsApplied;
    }
}
