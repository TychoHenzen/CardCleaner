using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.WangSets;

/// <summary>
/// Data container for parsed Wang sets.
/// </summary>
internal sealed class WangSetData
{
    /// <summary>
    /// Maps (wangSetName, bitmask) to the list of tiles with that pattern.
    /// Multiple tiles = variations.
    /// </summary>
    internal Dictionary<(string setName, int bitmask), List<WangTileInfo>> BitmaskToTiles { get; } = new();

    /// <summary>
    /// Maps tileId to the wang set it belongs to.
    /// </summary>
    internal Dictionary<int, string> TileToWangSet { get; } = new();

    /// <summary>
    /// Maps tileId to the bitmask type for that wang set.
    /// </summary>
    internal Dictionary<int, BitmaskType> TileBitmaskType { get; } = new();

    /// <summary>
    /// Maps wang set name to info about the set (bitmask type, properties).
    /// </summary>
    internal Dictionary<string, WangSetInfo> WangSetInfo { get; } = new();
}
