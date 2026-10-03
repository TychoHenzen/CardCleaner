#if TOOLS
using System;
using System.Collections.Generic;
using CardCleaner.Addons.TileEditor;
using Godot;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal static class TmxAtlasImageOperations
{
    internal static Image? CreateAtlasImage(
        List<PackedTsxTile> packedTiles,
        Vector2I atlasSize,
        int targetTileSize)
    {
        try
        {
            var atlasImage = Image.CreateEmpty(atlasSize.X, atlasSize.Y, false, Image.Format.Rgba8);
            atlasImage.Fill(new Color(0, 0, 0, 0));
            foreach (var packed in packedTiles)
                AddTileToAtlas(atlasImage, packed, targetTileSize);
            return atlasImage;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TmxAtlasCompiler] Error creating atlas: {ex.Message}");
            return null;
        }
    }

    internal static CompositePackResult PackCompositeToAtlas(
        Image atlasImage,
        Image compositeImage,
        int targetTileSize,
        int currentX,
        int currentY,
        int rowHeight,
        int atlasWidth)
    {
        if (currentX + targetTileSize > atlasWidth)
        {
            currentX = 0;
            currentY += rowHeight;
        }

        if (currentY + targetTileSize > atlasImage.GetHeight())
            atlasImage = ExpandAtlas(atlasImage);

        atlasImage.BlitRect(
            compositeImage,
            new Rect2I(0, 0, targetTileSize, targetTileSize),
            new Vector2I(currentX, currentY));
        return new CompositePackResult
        {
            Atlas = atlasImage,
            CurrentX = currentX,
            CurrentY = currentY
        };
    }

    internal static Image ExtractTileRegion(TsxTileData tile, int targetTileSize)
        => ExtractTileRegion(
            tile.SourceImage,
            tile.AtlasX,
            tile.AtlasY,
            tile.SourceTileWidth,
            tile.SourceTileHeight,
            targetTileSize);

    internal static Image ExtractTileRegion(
        TsxWangSetData wangSet,
        int tileId,
        int targetTileSize)
        => ExtractTileRegion(
            wangSet.SourceImage,
            tileId % wangSet.Columns,
            tileId / wangSet.Columns,
            wangSet.SourceTileWidth,
            wangSet.SourceTileHeight,
            targetTileSize);

    internal static Image CompositeImages(Image baseImage, Image borderImage)
    {
        var width = baseImage.GetWidth();
        var height = baseImage.GetHeight();
        if (borderImage.GetWidth() != width || borderImage.GetHeight() != height)
        {
            var resizedBorder = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
            resizedBorder.BlitRect(
                borderImage,
                new Rect2I(0, 0, borderImage.GetWidth(), borderImage.GetHeight()),
                Vector2I.Zero);
            borderImage = resizedBorder;
        }

        var result = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        result.BlitRect(baseImage, new Rect2I(0, 0, width, height), Vector2I.Zero);
        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
            BlendPixel(result, borderImage, x, y);
        return result;
    }

    private static void AddTileToAtlas(
        Image atlasImage,
        PackedTsxTile packed,
        int targetTileSize)
    {
        var tile = packed.Source;
        var sourceRect = new Rect2I(
            tile.AtlasX * tile.SourceTileWidth,
            tile.AtlasY * tile.SourceTileHeight,
            tile.SourceTileWidth,
            tile.SourceTileHeight);
        var extractedTile = Image.CreateEmpty(
            tile.SourceTileWidth,
            tile.SourceTileHeight,
            false,
            Image.Format.Rgba8);
        extractedTile.BlitRect(tile.SourceImage, sourceRect, Vector2I.Zero);

        if (tile.SourceTileWidth != targetTileSize || tile.SourceTileHeight != targetTileSize)
        {
            var interpolation = GetScalingInterpolation(tile.SourceTileWidth, tile.SourceTileHeight);
            extractedTile.Resize(targetTileSize, targetTileSize, interpolation);
        }

        atlasImage.BlitRect(
            extractedTile,
            new Rect2I(0, 0, targetTileSize, targetTileSize),
            new Vector2I(packed.AtlasX, packed.AtlasY));
    }

    private static Image ExpandAtlas(Image atlasImage)
    {
        var newHeight = Math.Min(atlasImage.GetHeight() * 2, 16384);
        var expandedAtlas = Image.CreateEmpty(
            atlasImage.GetWidth(),
            newHeight,
            false,
            Image.Format.Rgba8);
        expandedAtlas.Fill(new Color(0, 0, 0, 0));
        expandedAtlas.BlitRect(
            atlasImage,
            new Rect2I(0, 0, atlasImage.GetWidth(), atlasImage.GetHeight()),
            Vector2I.Zero);
        return expandedAtlas;
    }

    private static Image ExtractTileRegion(
        Image sourceImage,
        int atlasX,
        int atlasY,
        int sourceTileWidth,
        int sourceTileHeight,
        int targetTileSize)
    {
        var sourceRect = new Rect2I(
            atlasX * sourceTileWidth,
            atlasY * sourceTileHeight,
            sourceTileWidth,
            sourceTileHeight);
        var extracted = Image.CreateEmpty(
            sourceTileWidth,
            sourceTileHeight,
            false,
            Image.Format.Rgba8);
        extracted.BlitRect(sourceImage, sourceRect, Vector2I.Zero);
        ResizeIfNeeded(extracted, sourceTileWidth, sourceTileHeight, targetTileSize);
        return extracted;
    }

    private static void ResizeIfNeeded(
        Image image,
        int sourceTileWidth,
        int sourceTileHeight,
        int targetTileSize)
    {
        if (sourceTileWidth == targetTileSize && sourceTileHeight == targetTileSize)
            return;

        image.Resize(
            targetTileSize,
            targetTileSize,
            GetScalingInterpolation(sourceTileWidth, sourceTileHeight));
    }

    private static void BlendPixel(Image result, Image borderImage, int x, int y)
    {
        var baseColor = result.GetPixel(x, y);
        var borderColor = borderImage.GetPixel(x, y);
        var alpha = borderColor.A;
        if (alpha <= 0)
            return;

        var blended = new Color(
            borderColor.R * alpha + baseColor.R * (1 - alpha),
            borderColor.G * alpha + baseColor.G * (1 - alpha),
            borderColor.B * alpha + baseColor.B * (1 - alpha),
            Math.Max(baseColor.A, alpha));
        result.SetPixel(x, y, blended);
    }

    private static Image.Interpolation GetScalingInterpolation(int sourceWidth, int sourceHeight)
        => IsPowerOfTwo(sourceWidth) && IsPowerOfTwo(sourceHeight)
            ? Image.Interpolation.Nearest
            : Image.Interpolation.Bilinear;

    private static bool IsPowerOfTwo(int value)
        => value > 0 && (value & (value - 1)) == 0;
}
#endif
