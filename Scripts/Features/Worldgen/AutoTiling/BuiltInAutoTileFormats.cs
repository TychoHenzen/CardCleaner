using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Features.Worldgen.AutoTiling;

/// <summary>
/// Factory methods for creating the built-in auto-tile format definitions.
/// These formats are automatically registered in <see cref="AutoTileFormatRegistry"/>
/// on first access.
/// </summary>
public static class BuiltInAutoTileFormats
{
    /// <summary>
    /// Creates the Corner16 format definition.
    /// 4-bit diagonal corner format with 16 variants (0-15).
    /// Checks NE=1, SE=2, SW=4, NW=8 neighbors.
    /// </summary>
    public static AutoTileFormatDefinition CreateCorner16()
    {
        var bitmasks = Enumerable.Range(0, 16).ToHashSet();

        // Built-in formats use placeholder variant mappings - actual atlas coords
        // are defined per-tile in the tile definitions, not in the format itself.
        // The format defines which bitmasks are valid; tiles define the atlas positions.
        var variants = new Dictionary<int, VariantDefinition>();
        for (var i = 0; i < 16; i++)
        {
            variants[i] = new VariantDefinition(new Vector2I(i, 0));
        }

        return new AutoTileFormatDefinition(
            name: "corner16",
            bitmaskType: BitmaskType.Corner4,
            allowedBitmasks: bitmasks,
            variantMappings: variants,
            isBuiltIn: true);
    }

    /// <summary>
    /// Creates the Edge16 format definition.
    /// 4-bit cardinal edge format with 16 variants (0-15).
    /// Checks N=1, E=2, S=4, W=8 neighbors.
    /// </summary>
    public static AutoTileFormatDefinition CreateEdge16()
    {
        var bitmasks = Enumerable.Range(0, 16).ToHashSet();

        var variants = new Dictionary<int, VariantDefinition>();
        for (var i = 0; i < 16; i++)
        {
            variants[i] = new VariantDefinition(new Vector2I(i, 0));
        }

        return new AutoTileFormatDefinition(
            name: "edge16",
            bitmaskType: BitmaskType.Edge4,
            allowedBitmasks: bitmasks,
            variantMappings: variants,
            isBuiltIn: true);
    }

    /// <summary>
    /// Creates the Blob47 format definition.
    /// 8-bit full neighbor format with 47 valid variants.
    /// Uses all 8 neighbors (N, NE, E, SE, S, SW, W, NW) but only combinations
    /// where corners are valid (both adjacent edges present) are allowed.
    /// </summary>
    public static AutoTileFormatDefinition CreateBlob47()
    {
        var validMasks = GetValidBlob47Bitmasks();
        var bitmasks = validMasks.ToHashSet();

        var variants = new Dictionary<int, VariantDefinition>();
        var index = 0;
        foreach (var mask in validMasks)
        {
            variants[mask] = new VariantDefinition(new Vector2I(index, 0));
            index++;
        }

        return new AutoTileFormatDefinition(
            name: "blob47",
            bitmaskType: BitmaskType.Full8,
            allowedBitmasks: bitmasks,
            variantMappings: variants,
            isBuiltIn: true);
    }

    /// <summary>
    /// Gets the 47 valid blob bitmask values in the canonical order.
    /// This order matches what <see cref="NeighborBitmask8.GetBlobIndex"/> expects.
    /// </summary>
    private static List<int> GetValidBlob47Bitmasks()
    {
        var valid = new List<int>(47);

        // Bit positions: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128
        for (var mask = 0; mask < 256; mask++)
        {
            if (IsValidBlobMask(mask))
                valid.Add(mask);
        }

        return valid;
    }

    /// <summary>
    /// Checks if an 8-bit bitmask represents a valid blob configuration.
    /// A corner is only valid if both adjacent edges are present.
    /// </summary>
    private static bool IsValidBlobMask(int mask)
    {
        const int N = 1, NE = 2, E = 4, SE = 8, S = 16, SW = 32, W = 64, NW = 128;

        // NE corner requires N and E edges
        if ((mask & NE) != 0 && ((mask & N) == 0 || (mask & E) == 0))
            return false;

        // SE corner requires E and S edges
        if ((mask & SE) != 0 && ((mask & E) == 0 || (mask & S) == 0))
            return false;

        // SW corner requires S and W edges
        if ((mask & SW) != 0 && ((mask & S) == 0 || (mask & W) == 0))
            return false;

        // NW corner requires W and N edges
        if ((mask & NW) != 0 && ((mask & W) == 0 || (mask & N) == 0))
            return false;

        return true;
    }
}
