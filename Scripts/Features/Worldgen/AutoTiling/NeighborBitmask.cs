using System;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Utility for computing 4-bit neighbor bitmasks for auto-tiling.
///     Uses NESW convention: N=1, E=2, S=4, W=8.
/// </summary>
public static class NeighborBitmask
{
    /// <summary>North neighbor bit (0b0001)</summary>
    public const int North = 1;

    /// <summary>East neighbor bit (0b0010)</summary>
    public const int East = 2;

    /// <summary>South neighbor bit (0b0100)</summary>
    public const int South = 4;

    /// <summary>West neighbor bit (0b1000)</summary>
    public const int West = 8;

    /// <summary>
    ///     Direction offsets for NESW, matching bitmask bit order.
    ///     Index 0 = North (0,-1), Index 1 = East (1,0), Index 2 = South (0,1), Index 3 = West (-1,0)
    /// </summary>
    public static readonly Vector2I[] Directions =
    {
        new(0, -1), // North
        new(1, 0), // East
        new(0, 1), // South
        new(-1, 0) // West
    };

    /// <summary>
    ///     Human-readable names for each bitmask value (0-15).
    /// </summary>
    public static readonly string[] BitmaskNames =
    {
        "None (0)", "N (1)", "E (2)", "N+E (3)", "S (4)", "N+S (5)", "E+S (6)", "N+E+S (7)", "W (8)", "N+W (9)",
        "E+W (10)", "N+E+W (11)", "S+W (12)", "N+S+W (13)", "E+S+W (14)", "NESW (15)"
    };

    /// <summary>
    ///     Compute the 4-bit neighbor bitmask for a position.
    /// </summary>
    /// <param name="position">The tile position to check</param>
    /// <param name="isSameTerrain">Function that returns true if neighbor at position matches the base terrain</param>
    /// <returns>4-bit bitmask (0-15) indicating which neighbors match</returns>
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
    ///     Compute the 4-bit neighbor bitmask using a tile ID map.
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
    ///     Compute the 4-bit neighbor bitmask using a 2D string array.
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
                return null; // Out of bounds - doesn't match

            return tileIds[neighborPos.Y, neighborPos.X];
        });
    }

    /// <summary>
    ///     Check if a specific direction bit is set in a bitmask.
    /// </summary>
    public static bool HasDirection(int bitmask, int direction) => (bitmask & direction) != 0;

    /// <summary>
    ///     Check if north neighbor is set.
    /// </summary>
    public static bool HasNorth(int bitmask) => HasDirection(bitmask, North);

    /// <summary>
    ///     Check if east neighbor is set.
    /// </summary>
    public static bool HasEast(int bitmask) => HasDirection(bitmask, East);

    /// <summary>
    ///     Check if south neighbor is set.
    /// </summary>
    public static bool HasSouth(int bitmask) => HasDirection(bitmask, South);

    /// <summary>
    ///     Check if west neighbor is set.
    /// </summary>
    public static bool HasWest(int bitmask) => HasDirection(bitmask, West);

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
