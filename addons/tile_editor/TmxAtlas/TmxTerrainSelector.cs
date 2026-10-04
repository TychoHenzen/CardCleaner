#if TOOLS
using System.Collections.Generic;
using System.Linq;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal static class TmxTerrainSelector
{
    internal static TerrainSelection Create(
        List<TsxTileData> allTiles,
        List<TsxWangSetData> allWangSets)
    {
        var simpleBaseTerrains = FindSimpleBaseTerrains(allTiles, allWangSets);
        var compositableWangSets = allWangSets
            .Where(IsCompositable)
            .ToList();
        var fixedWangSets = allWangSets
            .Where(wangSet => !IsCompositable(wangSet))
            .ToList();
        var wangSetSolidFills = FindSolidFills(allTiles, compositableWangSets);
        var baseTerrains = simpleBaseTerrains.Concat(wangSetSolidFills).ToList();
        var tilesToPack = FindTilesToPack(allTiles, compositableWangSets);

        return new TerrainSelection
        {
            SimpleBaseTerrains = simpleBaseTerrains,
            WangSetSolidFills = wangSetSolidFills,
            BaseTerrains = baseTerrains,
            CompositableWangSets = compositableWangSets,
            FixedWangSets = fixedWangSets,
            TilesToPack = tilesToPack,
            EstimatedCompositeCount = EstimateCompositeCount(baseTerrains, compositableWangSets)
        };
    }

    private static List<TsxTileData> FindSimpleBaseTerrains(
        List<TsxTileData> allTiles,
        List<TsxWangSetData> allWangSets)
        => allTiles
            .Where(tile => tile.HasClassAttribute
                && tile.Layer.ToLowerInvariant() == "terrain"
                && !allWangSets.Any(wangSet =>
                    wangSet.AllMemberTileIds.Contains(tile.TileId)
                    && wangSet.TsxPath == tile.TsxPath))
            .GroupBy(tile => tile.Id)
            .Select(group => group.First())
            .ToList();

    private static bool IsCompositable(TsxWangSetData wangSet)
        => wangSet.OuterTerrain == "*" && wangSet.IsTransparent;

    private static List<TsxTileData> FindSolidFills(
        List<TsxTileData> allTiles,
        List<TsxWangSetData> compositableWangSets)
    {
        var solidFills = new List<TsxTileData>();
        foreach (var wangSet in compositableWangSets)
        {
            if (!string.IsNullOrEmpty(wangSet.InnerTerrain) && wangSet.InnerTerrain != "$self")
                continue;

            if (!wangSet.WangTiles.TryGetValue(15, out var solidTileIds) || solidTileIds.Count == 0)
                continue;

            var solidTile = allTiles.FirstOrDefault(tile =>
                tile.TileId == solidTileIds[0] && tile.TsxPath == wangSet.TsxPath);
            if (solidTile != null)
                solidFills.Add(CreateSolidFill(wangSet, solidTile));
        }

        return solidFills;
    }

    private static TsxTileData CreateSolidFill(TsxWangSetData wangSet, TsxTileData solidTile)
        => new()
        {
            TileId = solidTile.TileId,
            TsxPath = wangSet.TsxPath,
            AtlasX = solidTile.TileId % wangSet.Columns,
            AtlasY = solidTile.TileId / wangSet.Columns,
            SourceImage = wangSet.SourceImage,
            SourceTileWidth = wangSet.SourceTileWidth,
            SourceTileHeight = wangSet.SourceTileHeight,
            SourceScale = wangSet.SourceScale,
            Id = TmxAtlasNames.ToSnakeCase(wangSet.Name),
            Layer = "terrain",
            Dominance = 0,
            Properties = solidTile.Properties
        };

    private static List<TsxTileData> FindTilesToPack(
        List<TsxTileData> allTiles,
        List<TsxWangSetData> compositableWangSets)
        => allTiles
            .Where(tile => !compositableWangSets.Any(wangSet =>
                wangSet.AllMemberTileIds.Contains(tile.TileId)
                && wangSet.TsxPath == tile.TsxPath))
            .ToList();

    private static int EstimateCompositeCount(
        List<TsxTileData> baseTerrains,
        List<TsxWangSetData> compositableWangSets)
    {
        var estimated = 0;
        foreach (var wangSet in compositableWangSets)
        {
            var baseCount = baseTerrains.Count(baseTerrain =>
                baseTerrain.Id != TmxAtlasNames.ToSnakeCase(wangSet.Name));
            var variants = wangSet.WangTiles.Values.Sum(tileIds => tileIds.Count);
            estimated += baseCount * (variants + 16 + 2);
        }

        return estimated;
    }
}
#endif
