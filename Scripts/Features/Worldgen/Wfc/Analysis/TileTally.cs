using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Running per-tile-type counts and region counts gathered while scanning a tile map.
/// </summary>
internal sealed class TileTally
{
    internal Dictionary<string, int> TileCounts { get; } = new();
    internal Dictionary<string, int> TileRegions { get; } = new();
    internal int TotalTiles { get; private set; }

    internal void RecordTile(string tile)
    {
        TotalTiles++;

        if (!TileCounts.TryAdd(tile, 1))
            TileCounts[tile]++;
    }

    internal void RecordRegion(string tile)
    {
        if (!TileRegions.TryAdd(tile, 1))
            TileRegions[tile]++;
    }
}
