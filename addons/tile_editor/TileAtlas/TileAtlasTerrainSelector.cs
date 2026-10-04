#if TOOLS
using System.Collections.Generic;
using System.Linq;
using static CardCleaner.Addons.TileEditor.TileAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TileAtlasTerrainSelector
{
    internal TerrainSelection Select(IEnumerable<EditableTile> sourceTiles)
    {
        var tiles = sourceTiles.ToList();
        var simpleBaseTerrains = tiles
            .Where(tile => !tile.HasAutoTileVariants && tile.Layer == "terrain")
            .ToList();
        var compositableAutoTiles = tiles
            .Where(tile => tile.HasAutoTileVariants && tile.IsCompositable)
            .ToList();
        var fixedAutoTiles = tiles
            .Where(tile => tile.HasAutoTileVariants && !tile.IsCompositable)
            .ToList();
        var autoTileSolidFills = compositableAutoTiles
            .Where(tile => string.IsNullOrEmpty(tile.InnerTerrainId))
            .ToList();

        return new TerrainSelection
        {
            SimpleBaseTerrains = simpleBaseTerrains,
            AutoTileSolidFills = autoTileSolidFills,
            BaseTerrains = simpleBaseTerrains.Concat(autoTileSolidFills).ToList(),
            CompositableAutoTiles = compositableAutoTiles,
            FixedAutoTiles = fixedAutoTiles
        };
    }

    internal List<TileRegion> CollectTilesToPack(IEnumerable<EditableTile> sourceTiles)
    {
        var regions = new List<TileRegion>();
        var seenRegions = new HashSet<string>();

        foreach (var tile in sourceTiles)
        {
            if (!tile.IsCompositable)
                AddRegionIfNew(regions, seenRegions, CreateBaseRegion(tile));

            if (tile.AutoTileVariants != null && !tile.IsCompositable)
            {
                foreach (var variant in tile.AutoTileVariants)
                {
                    if (variant.HasValue)
                    {
                        AddRegionIfNew(
                            regions,
                            seenRegions,
                            CreateVariantRegion(tile, variant.Value));
                    }
                }
            }

            AddVisualVariationRegions(tile, regions, seenRegions);
            AddAnimationRegions(tile, regions, seenRegions);
        }

        return regions;
    }

    private static TileRegion CreateBaseRegion(EditableTile tile)
    {
        return new TileRegion(
            tile.SourceId,
            tile.AtlasX,
            tile.AtlasY,
            tile.SizeX,
            tile.SizeY,
            tile.SourceScale);
    }

    private static TileRegion CreateVariantRegion(EditableTile tile, Godot.Vector2I coordinates)
    {
        return new TileRegion(
            tile.SourceId,
            coordinates.X,
            coordinates.Y,
            1,
            1,
            tile.SourceScale);
    }

    private static void AddVisualVariationRegions(
        EditableTile tile,
        List<TileRegion> regions,
        HashSet<string> seenRegions)
    {
        if (tile.Variations == null)
            return;

        foreach (var variation in tile.Variations)
        {
            AddRegionIfNew(
                regions,
                seenRegions,
                new TileRegion(
                    tile.SourceId,
                    variation.X,
                    variation.Y,
                    tile.SizeX,
                    tile.SizeY,
                    tile.SourceScale));
        }
    }

    private static void AddAnimationRegions(
        EditableTile tile,
        List<TileRegion> regions,
        HashSet<string> seenRegions)
    {
        if (tile.AnimationFrames == null)
            return;

        foreach (var frame in tile.AnimationFrames)
        {
            AddRegionIfNew(
                regions,
                seenRegions,
                new TileRegion(
                    tile.SourceId,
                    frame.X,
                    frame.Y,
                    tile.SizeX,
                    tile.SizeY,
                    tile.SourceScale));
        }
    }

    private static void AddRegionIfNew(
        List<TileRegion> regions,
        HashSet<string> seenRegions,
        TileRegion region)
    {
        var key = $"{region.SourceId}:{region.AtlasX},{region.AtlasY}";
        if (!seenRegions.Add(key))
            return;

        regions.Add(region);
    }
}
#endif
