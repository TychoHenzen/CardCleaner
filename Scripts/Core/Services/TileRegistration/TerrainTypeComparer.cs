using System;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Decides whether two tile IDs represent the same terrain type, treating variants of one
/// variation group and auto-tiles that share a variants array as equivalent.
/// </summary>
internal static class TerrainTypeComparer
{
    internal static bool AreSame(
        string? tileId1,
        string? tileId2,
        VariationGroupCollection variationGroups,
        Func<string, TileDefinition?> getTile)
    {
        // Both null or empty = same (both empty)
        if (string.IsNullOrEmpty(tileId1) && string.IsNullOrEmpty(tileId2))
            return true;

        // One null/empty, other not = different
        if (string.IsNullOrEmpty(tileId1) || string.IsNullOrEmpty(tileId2))
            return false;

        // Exact match
        if (tileId1 == tileId2)
            return true;

        // Check if both tiles are in the same variation group
        var group1 = variationGroups.FindGroupContaining(tileId1);
        var group2 = variationGroups.FindGroupContaining(tileId2);

        if (group1 != null && group2 != null)
            return group1.BaseName == group2.BaseName;

        // Not in variation groups - compare by tile definition's auto-tile equivalence
        // Two auto-tiles with the same variants array pointer are equivalent
        var tile1 = getTile(tileId1);
        var tile2 = getTile(tileId2);

        if (tile1?.AutoTileVariants != null && tile2?.AutoTileVariants != null)
            return ReferenceEquals(tile1.AutoTileVariants, tile2.AutoTileVariants);

        return false;
    }
}
