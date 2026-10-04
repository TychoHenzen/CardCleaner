using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.WangSets;

/// <summary>
/// A Tiled wangid decoded into "terrain present" flags. Tiled orders the eight positions
/// N, NE, E, SE, S, SW, W, NW; position i is stored in bit i of the mask.
/// </summary>
internal readonly struct WangId
{
    private readonly int _presentMask;

    private WangId(int presentMask)
    {
        _presentMask = presentMask;
    }

    private const int PositionCount = 8;
    private const int North = 0;
    private const int NorthEast = 1;
    private const int East = 2;
    private const int SouthEast = 3;
    private const int South = 4;
    private const int SouthWest = 5;
    private const int West = 6;
    private const int NorthWest = 7;

    /// <summary>
    /// Decodes a wangid string; returns null when it does not hold exactly eight values.
    /// </summary>
    internal static WangId? Parse(string wangidStr, int terrainColorIndex)
    {
        var parts = wangidStr.Split(',');
        if (parts.Length != PositionCount) return null;

        var mask = 0;
        for (var i = 0; i < PositionCount; i++)
        {
            if (ParseHelpers.ParseInt(parts[i], 0) == terrainColorIndex)
                mask |= 1 << i;
        }

        return new WangId(mask);
    }

    /// <summary>
    /// Converts to a Full8 bitmask for the given Tiled wang type. Corner and mixed sets derive
    /// edges from the corners; edge sets use only the four edges.
    /// </summary>
    internal int ToBitmask(string wangType) => wangType switch
    {
        "corner" or "mixed" => DualGridAutoTile.CornersToFull8Bitmask(
            Has(NorthEast), Has(SouthEast), Has(SouthWest), Has(NorthWest)),
        "edge" => EdgeBitmask(),
        _ => 0
    };

    /// <summary>
    /// True when the edges match what the corners imply (an edge is set if either adjacent corner is set).
    /// Dual-grid auto-tiling only wants these canonical tiles.
    /// </summary>
    internal bool IsBlobCompliant() =>
        Has(North) == (Has(NorthWest) || Has(NorthEast))
        && Has(East) == (Has(NorthEast) || Has(SouthEast))
        && Has(South) == (Has(SouthEast) || Has(SouthWest))
        && Has(West) == (Has(SouthWest) || Has(NorthWest));

    private bool Has(int position) => (_presentMask & (1 << position)) != 0;

    private int EdgeBitmask()
    {
        var bitmask = 0;
        if (Has(North)) bitmask |= NeighborBitmask8.North;
        if (Has(East)) bitmask |= NeighborBitmask8.East;
        if (Has(South)) bitmask |= NeighborBitmask8.South;
        if (Has(West)) bitmask |= NeighborBitmask8.West;
        return bitmask;
    }
}
