using CardCleaner.Scripts.Core.Services.TilesetLoading.WangSets;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;

/// <summary>
/// Queries parsed Wang set data for representative tiles and auto-tile variants.
/// </summary>
internal static class WangSetTileLookup
{
    // Wang data stores tiles at Full8 keys from WangIdToBitmask; 255 is the "full" tile.
    private const int FullBitmask = 255;
    private const int Corner16VariantCount = 16;

    /// <summary>
    /// Representative tile of a Wang set: the full tile when present, otherwise the first tile found.
    /// </summary>
    internal static WangTileInfo? FindRepresentativeTile(string setName, WangSetData wangData)
    {
        if (wangData.BitmaskToTiles.TryGetValue((setName, FullBitmask), out var fullTiles) && fullTiles.Count > 0)
            return fullTiles[0];

        foreach (var ((name, _), tiles) in wangData.BitmaskToTiles)
        {
            if (name == setName && tiles.Count > 0)
                return tiles[0];
        }

        return null;
    }

    /// <summary>
    /// Build auto-tile variant array from Wang set data.
    /// For bitmasks with multiple tiles, picks the first (variations handled separately).
    /// </summary>
    internal static Vector2I?[]? BuildAutoTileVariants(string wangSetName, TileBuildContext context)
    {
        // Dual-grid only samples 4 corners, so we always use Corner16 format (16 variants)
        // For blob47/mixed tilesets, map Full8 bitmasks back to their Corner16 equivalents
        var variants = new Vector2I?[Corner16VariantCount];
        var hasAnyVariant = false;

        for (var corner16 = 0; corner16 < Corner16VariantCount; corner16++)
        {
            var full8Mask = DualGridAutoTile.CornersToFull8Bitmask(
                (corner16 & NeighborBitmaskCorner.NorthEast) != 0,
                (corner16 & NeighborBitmaskCorner.SouthEast) != 0,
                (corner16 & NeighborBitmaskCorner.SouthWest) != 0,
                (corner16 & NeighborBitmaskCorner.NorthWest) != 0);

            if (!context.WangData.BitmaskToTiles.TryGetValue((wangSetName, full8Mask), out var tilesForMask)
                || tilesForMask.Count == 0)
                continue;

            variants[corner16] = ParseHelpers.TileIdToAtlasCoords(tilesForMask[0].TileId, context.Columns);
            hasAnyVariant = true;
        }

        return hasAnyVariant ? variants : null;
    }
}
