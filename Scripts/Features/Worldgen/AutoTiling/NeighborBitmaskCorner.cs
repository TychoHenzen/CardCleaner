using System;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Utility for computing 4-bit corner neighbor bitmasks for auto-tiling.
///     Uses diagonal corners: NE=1, SE=2, SW=4, NW=8.
///     This is the standard 16-tile corner-based format.
/// </summary>
public static class NeighborBitmaskCorner
{
    /// <summary>NE corner bit (0b0001)</summary>
    public const int NorthEast = 1;

    /// <summary>SE corner bit (0b0010)</summary>
    public const int SouthEast = 2;

    /// <summary>SW corner bit (0b0100)</summary>
    public const int SouthWest = 4;

    /// <summary>NW corner bit (0b1000)</summary>
    public const int NorthWest = 8;

    /// <summary>
    ///     Direction offsets for diagonal corners, matching bitmask bit order.
    ///     Index 0 = NE (1,-1), Index 1 = SE (1,1), Index 2 = SW (-1,1), Index 3 = NW (-1,-1)
    /// </summary>
    public static readonly Vector2I[] Directions =
    {
        new(1, -1),  // NE
        new(1, 1),   // SE
        new(-1, 1),  // SW
        new(-1, -1)  // NW
    };

    /// <summary>
    ///     Human-readable names for each bitmask value (0-15).
    /// </summary>
    public static readonly string[] BitmaskNames =
    {
        "None (0)",
        "NE (1)",
        "SE (2)",
        "NE+SE (3)",
        "SW (4)",
        "NE+SW (5)",
        "SE+SW (6)",
        "NE+SE+SW (7)",
        "NW (8)",
        "NE+NW (9)",
        "SE+NW (10)",
        "NE+SE+NW (11)",
        "SW+NW (12)",
        "NE+SW+NW (13)",
        "SE+SW+NW (14)",
        "All (15)"
    };

    /// <summary>
    ///     Compute the 4-bit corner neighbor bitmask for a position.
    /// </summary>
    /// <param name="position">The tile position to check</param>
    /// <param name="isSameTerrain">Function that returns true if neighbor at position matches the base terrain</param>
    /// <returns>4-bit bitmask (0-15) indicating which diagonal neighbors match</returns>
    public static int Compute(Vector2I position, Func<Vector2I, bool> isSameTerrain)
    {
        var mask = 0;

        for (var i = 0; i < 4; i++)
        {
            var neighborPos = position + Directions[i];
            if (isSameTerrain(neighborPos)) mask |= 1 << i;
        }

        return mask;
    }

    /// <summary>
    ///     Compute the 4-bit corner neighbor bitmask using a tile ID map.
    /// </summary>
    /// <param name="position">The tile position to check</param>
    /// <param name="baseTileId">The tile ID to match against</param>
    /// <param name="getTileId">Function that returns tile ID at a position, or null if out of bounds</param>
    /// <returns>4-bit bitmask (0-15)</returns>
    public static int Compute(Vector2I position, string baseTileId, Func<Vector2I, string?> getTileId)
    {
        return Compute(position, neighborPos =>
        {
            var neighborTileId = getTileId(neighborPos);
            return neighborTileId == baseTileId;
        });
    }

    /// <summary>
    ///     Compute the 4-bit corner neighbor bitmask using a 2D string array.
    ///     Handles boundary checking - out-of-bounds positions are treated as non-matching.
    /// </summary>
    /// <param name="position">The tile position to check</param>
    /// <param name="baseTileId">The tile ID to match against</param>
    /// <param name="tileIds">2D array of tile IDs indexed [y, x]</param>
    /// <param name="mapSize">Size of the map</param>
    /// <returns>4-bit bitmask (0-15)</returns>
    public static int Compute(Vector2I position, string baseTileId, string[,] tileIds, Vector2I mapSize)
    {
        return Compute(position, baseTileId, neighborPos =>
        {
            if (neighborPos.X < 0 || neighborPos.X >= mapSize.X ||
                neighborPos.Y < 0 || neighborPos.Y >= mapSize.Y)
                return null;

            return tileIds[neighborPos.Y, neighborPos.X];
        });
    }

    /// <summary>
    ///     Check if a specific corner bit is set in a bitmask.
    /// </summary>
    public static bool HasCorner(int bitmask, int corner) => (bitmask & corner) != 0;

    /// <summary>Check if NE corner is set.</summary>
    public static bool HasNorthEast(int bitmask) => HasCorner(bitmask, NorthEast);

    /// <summary>Check if SE corner is set.</summary>
    public static bool HasSouthEast(int bitmask) => HasCorner(bitmask, SouthEast);

    /// <summary>Check if SW corner is set.</summary>
    public static bool HasSouthWest(int bitmask) => HasCorner(bitmask, SouthWest);

    /// <summary>Check if NW corner is set.</summary>
    public static bool HasNorthWest(int bitmask) => HasCorner(bitmask, NorthWest);

    /// <summary>
    ///     Get a human-readable description of a bitmask.
    /// </summary>
    public static string GetDescription(int bitmask)
    {
        if (bitmask < 0 || bitmask > 15)
            return $"Invalid ({bitmask})";

        return BitmaskNames[bitmask];
    }
}
