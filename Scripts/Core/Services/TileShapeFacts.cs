using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>Resolves the shape facts of an auto-tile format name once, when a tile is built.</summary>
internal static class TileShapeFacts
{
    internal static (IReadOnlySet<int>? AllowedBitmasks, (Vector2I Size, Vector2I Offset)? MultiCellBounds)
        Resolve(string formatName)
    {
        if (!AutoTileFormatRegistry.TryGet(formatName, out var format) || format == null)
            return (null, null);
        return (format.AllowedBitmasks, format.GetMaxMultiCellBounds());
    }
}
