#if TOOLS
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Answers which bitmask values exist for a <see cref="BitmaskType"/>, how a bitmask is previewed,
/// and how many cardinal edges it sets.
/// </summary>
internal static class BitmaskTypeCatalog
{
    private const int FourBitMaskCount = 16;

    /// <summary>
    /// Gets every valid bitmask for the type: 0-15 for the 4-bit formats, the 47 blob masks for Full8.
    /// </summary>
    internal static int[] GetValidBitmasks(BitmaskType type)
    {
        IEnumerable<int> masks = type == BitmaskType.Full8
            ? NeighborBitmask8.GetValid47Masks()
            : Enumerable.Range(0, FourBitMaskCount);
        return masks.ToArray();
    }

    internal static TileShapePreview.Format GetPreviewFormat(BitmaskType type)
    {
        return type switch
        {
            BitmaskType.Corner4 => TileShapePreview.Format.Corner16,
            BitmaskType.Edge4 => TileShapePreview.Format.Edge16,
            BitmaskType.Full8 => TileShapePreview.Format.Blob47,
            _ => TileShapePreview.Format.Corner16
        };
    }

    /// <summary>
    /// Gets the number of cardinal edges set in a blob bitmask (N=1, E=4, S=16, W=64).
    /// </summary>
    internal static int GetEdgeCount(int bitmask)
    {
        return BitOperations.PopCount((uint)(bitmask & NeighborBitmask8.AllEdges));
    }
}
#endif
