using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
///     Utility for computing 8-bit neighbor bitmasks for blob-style auto-tiling.
///     Uses all 8 directions: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128.
///
///     The blob format enforces that corners are only valid when both adjacent edges are set.
///     This constraint reduces 256 possible combinations to 47 valid ones.
/// </summary>
public static class NeighborBitmask8
{
    /// <summary>
    ///     Direction offsets for all 8 directions, matching bitmask bit order.
    ///     Alternates: edge, corner, edge, corner, etc.
    /// </summary>
    public static readonly IReadOnlyList<Vector2I> Directions = new Vector2I[]
    {
        new(0, -1),  // N  (bit 0)
        new(1, -1),  // NE (bit 1)
        new(1, 0),   // E  (bit 2)
        new(1, 1),   // SE (bit 3)
        new(0, 1),   // S  (bit 4)
        new(-1, 1),  // SW (bit 5)
        new(-1, 0),  // W  (bit 6)
        new(-1, -1)  // NW (bit 7)
    };

    /// <summary>
    ///     Human-readable names for direction bits.
    /// </summary>
    public static readonly IReadOnlyList<string> DirectionNames = new string[]
    {
        "N", "NE", "E", "SE", "S", "SW", "W", "NW"
    };

    /// <summary>
    ///     The 47 valid blob masks in ascending order, built once and never mutated.
    /// </summary>
    private static readonly int[] Valid47 = BuildValid47();

    /// <summary>
    ///     Compute the raw 8-bit neighbor bitmask for a position.
    ///     Does NOT apply blob constraints - use NormalizeToBlobMask for that.
    /// </summary>
    /// <param name="position">The tile position to check</param>
    /// <param name="isSameTerrain">Function that returns true if neighbor at position matches</param>
    /// <returns>8-bit bitmask (0-255) indicating which neighbors match</returns>
    public static int ComputeRaw(Vector2I position, Func<Vector2I, bool> isSameTerrain)
    {
        var mask = 0;

        for (var i = 0; i < 8; i++)
        {
            var neighborPos = position + Directions[i];
            if (isSameTerrain(neighborPos)) mask |= 1 << i;
        }

        return mask;
    }

    /// <summary>
    ///     Compute the blob-normalized 8-bit neighbor bitmask for a position.
    ///     Corners are only set if both adjacent edges are also set.
    /// </summary>
    /// <param name="position">The tile position to check</param>
    /// <param name="isSameTerrain">Function that returns true if neighbor at position matches</param>
    /// <returns>Blob-normalized 8-bit bitmask (one of 47 valid values)</returns>
    public static int Compute(Vector2I position, Func<Vector2I, bool> isSameTerrain)
    {
        var raw = ComputeRaw(position, isSameTerrain);
        return NormalizeToBlobMask(raw);
    }

    /// <summary>
    ///     Compute using a 2D tile ID array.
    /// </summary>
    public static int Compute(Vector2I position, string baseTileId, string[,] tileIds, Vector2I mapSize)
    {
        return Compute(position, neighborPos =>
        {
            if (neighborPos.X < 0 || neighborPos.X >= mapSize.X ||
                neighborPos.Y < 0 || neighborPos.Y >= mapSize.Y)
                return false;

            return tileIds[neighborPos.Y, neighborPos.X] == baseTileId;
        });
    }

    /// <summary>
    ///     Normalize a raw 8-bit mask to a valid blob mask.
    ///     Corners are cleared if their adjacent edges are not both set.
    /// </summary>
    /// <param name="rawMask">Raw 8-bit neighbor mask</param>
    /// <returns>Blob-normalized mask (one of 47 valid values)</returns>
    public static int NormalizeToBlobMask(int rawMask)
    {
        return (rawMask & (int)Neighbor8.AllEdges)
               | KeepCornerIfEdgesSet(rawMask, Neighbor8.NorthEast, Neighbor8.North, Neighbor8.East)
               | KeepCornerIfEdgesSet(rawMask, Neighbor8.SouthEast, Neighbor8.East, Neighbor8.South)
               | KeepCornerIfEdgesSet(rawMask, Neighbor8.SouthWest, Neighbor8.South, Neighbor8.West)
               | KeepCornerIfEdgesSet(rawMask, Neighbor8.NorthWest, Neighbor8.West, Neighbor8.North);
    }

    /// <summary>
    ///     Check if a mask is a valid blob mask (corners only set when adjacent edges are set).
    /// </summary>
    public static bool IsValidBlobMask(int mask)
    {
        return NormalizeToBlobMask(mask) == (mask & (int)Neighbor8.All);
    }

    private static int KeepCornerIfEdgesSet(
        int mask, Neighbor8 corner, Neighbor8 edgeA, Neighbor8 edgeB)
    {
        var required = (int)(corner | edgeA | edgeB);
        return (mask & required) == required ? (int)corner : 0;
    }

    /// <summary>
    ///     Get all 47 valid blob mask values.
    ///     These are the only masks that can occur with
    ///     blob-normalized computation.
    /// </summary>
    public static IReadOnlyList<int> GetValid47Masks()
    {
        return Valid47;
    }

    private static int[] BuildValid47()
    {
        var masks = new List<int>(47);

        for (var i = 0; i < 256; i++)
        {
            if (IsValidBlobMask(i))
                masks.Add(i);
        }

        return masks.ToArray();
    }

    /// <summary>
    ///     Get the index (0-46) of a valid blob mask in the 47-tile array.
    ///     Returns -1 if the mask is not a valid blob mask.
    /// </summary>
    public static int GetBlobIndex(int mask)
    {
        var valid = GetValid47Masks();
        for (var i = 0; i < valid.Count; i++)
        {
            if (valid[i] == mask)
                return i;
        }
        return -1;
    }

    /// <summary>
    ///     Get a human-readable description of an 8-bit bitmask.
    /// </summary>
    public static string GetDescription(int bitmask)
    {
        if (bitmask == 0) return "None (0)";
        if (bitmask == (int)Neighbor8.All) return "All (255)";

        var parts = new List<string>();
        for (var i = 0; i < 8; i++)
        {
            if ((bitmask & (1 << i)) != 0)
                parts.Add(DirectionNames[i]);
        }

        return $"{string.Join("+", parts)} ({bitmask})";
    }

    // Helper methods for checking individual directions
    public static bool HasNorth(int mask) => (mask & (int)Neighbor8.North) != 0;
    public static bool HasNorthEast(int mask) =>
        (mask & (int)Neighbor8.NorthEast) != 0;
    public static bool HasEast(int mask) => (mask & (int)Neighbor8.East) != 0;
    public static bool HasSouthEast(int mask) =>
        (mask & (int)Neighbor8.SouthEast) != 0;
    public static bool HasSouth(int mask) => (mask & (int)Neighbor8.South) != 0;
    public static bool HasSouthWest(int mask) =>
        (mask & (int)Neighbor8.SouthWest) != 0;
    public static bool HasWest(int mask) => (mask & (int)Neighbor8.West) != 0;
    public static bool HasNorthWest(int mask) =>
        (mask & (int)Neighbor8.NorthWest) != 0;
}
