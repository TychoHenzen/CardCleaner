#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TmxAtlasPacker
{
    private const int MaxAtlasSize = 16384;

    internal TilePackResult PackTiles(
        List<TsxTileData> tiles,
        int targetTileSize,
        int targetWidth = 0)
    {
        var packed = new List<PackedTsxTile>();
        var mapping = new Dictionary<string, Dictionary<string, TileAtlasRect>>();
        var tilesToPack = GetUniqueTiles(tiles);
        var atlasWidth = targetWidth > 0
            ? targetWidth
            : CalculateOptimalAtlasWidth(tilesToPack.Count, targetTileSize);
        var currentX = 0;
        var currentY = 0;
        var rowHeight = targetTileSize;

        foreach (var tile in tilesToPack)
        {
            if (currentX + targetTileSize > atlasWidth)
            {
                currentX = 0;
                currentY += rowHeight;
                rowHeight = targetTileSize;
            }

            if (currentY + targetTileSize > MaxAtlasSize)
                return Failure(packed, mapping);

            packed.Add(new PackedTsxTile
            {
                Source = tile,
                AtlasX = currentX,
                AtlasY = currentY
            });
            AddMapping(mapping, tile, currentX, currentY, targetTileSize);
            currentX += targetTileSize;
        }

        return new TilePackResult
        {
            Success = true,
            PackedTiles = packed,
            AtlasSize = new Vector2I(
                atlasWidth,
                NextPowerOf2(currentY + rowHeight)),
            Mapping = mapping
        };
    }

    internal Image? CreateAtlasImage(
        List<PackedTsxTile> packedTiles,
        Vector2I atlasSize,
        int targetTileSize)
        => TmxAtlasImageOperations.CreateAtlasImage(packedTiles, atlasSize, targetTileSize);

    internal CompositePackResult PackCompositeToAtlas(
        Image atlasImage,
        Image compositeImage,
        int targetTileSize,
        int currentX,
        int currentY,
        int rowHeight,
        int atlasWidth)
        => TmxAtlasImageOperations.PackCompositeToAtlas(
            atlasImage,
            compositeImage,
            targetTileSize,
            currentX,
            currentY,
            rowHeight,
            atlasWidth);

    internal Image ExtractTileRegion(TsxTileData tile, int targetTileSize)
        => TmxAtlasImageOperations.ExtractTileRegion(tile, targetTileSize);

    internal Image ExtractTileRegion(TsxWangSetData wangSet, int tileId, int targetTileSize)
        => TmxAtlasImageOperations.ExtractTileRegion(wangSet, tileId, targetTileSize);

    internal static Image CompositeImages(Image baseImage, Image borderImage)
        => TmxAtlasImageOperations.CompositeImages(baseImage, borderImage);

    internal int CalculateOptimalAtlasWidth(int totalTiles, int tileSize)
    {
        if (totalTiles <= 0)
            return tileSize;

        var tilesPerSide = (int)Math.Ceiling(Math.Sqrt(totalTiles));
        return NextPowerOf2(tilesPerSide * tileSize);
    }

    private static List<TsxTileData> GetUniqueTiles(List<TsxTileData> tiles)
    {
        var uniqueTiles = new Dictionary<string, TsxTileData>();
        foreach (var tile in tiles)
        {
            var key = $"{tile.TsxPath}:{tile.AtlasX},{tile.AtlasY}";
            if (!uniqueTiles.ContainsKey(key))
                uniqueTiles[key] = tile;
        }

        return uniqueTiles.Values.ToList();
    }

    private static void AddMapping(
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping,
        TsxTileData tile,
        int atlasX,
        int atlasY,
        int targetTileSize)
    {
        if (!mapping.TryGetValue(tile.TsxPath, out var sourceMapping))
        {
            sourceMapping = new Dictionary<string, TileAtlasRect>();
            mapping[tile.TsxPath] = sourceMapping;
        }

        var coordKey = $"{tile.AtlasX},{tile.AtlasY}";
        sourceMapping[coordKey] = new TileAtlasRect
        {
            X = atlasX / targetTileSize,
            Y = atlasY / targetTileSize,
            W = 1,
            H = 1
        };
    }

    private static TilePackResult Failure(
        List<PackedTsxTile> packed,
        Dictionary<string, Dictionary<string, TileAtlasRect>> mapping)
        => new()
        {
            Message = $"Atlas exceeds {MaxAtlasSize}x{MaxAtlasSize}",
            PackedTiles = packed,
            Mapping = mapping,
            AtlasSize = Vector2I.Zero
        };

    private static int NextPowerOf2(int value)
    {
        var power = 1;
        while (power < value)
            power *= 2;
        return Math.Min(power, MaxAtlasSize);
    }
}
#endif
