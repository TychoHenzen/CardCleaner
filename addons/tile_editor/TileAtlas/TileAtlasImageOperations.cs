#if TOOLS
using System;
using System.Collections.Generic;
using Godot;
using static CardCleaner.Addons.TileEditor.TileAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TileAtlasImageOperations
{
    internal Image? CreateAtlasImage(
        IReadOnlyList<PackedTile> packedTiles,
        Vector2I atlasSize,
        TileSet tileSet,
        Vector2I tileSize)
    {
        try
        {
            var atlasImage = Image.CreateEmpty(
                atlasSize.X,
                atlasSize.Y,
                false,
                Image.Format.Rgba8);
            atlasImage.Fill(new Color(0, 0, 0, 0));

            foreach (var packedTile in packedTiles)
                RenderPackedTile(atlasImage, packedTile, tileSet, tileSize);

            return atlasImage;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TileAtlasCompiler] Error creating atlas: {ex.Message}");
            return null;
        }
    }

    internal Image TrimAtlas(TileAtlasCompositionResult composition, int packedAtlasHeight)
    {
        var finalHeight = composition.CurrentY
            + (composition.CurrentX > 0 ? composition.RowHeight : 0);
        finalHeight = TileAtlasCompilerConstants.NextPowerOf2(
            Math.Max(finalHeight, packedAtlasHeight));
        if (finalHeight >= composition.Atlas.GetHeight())
            return composition.Atlas;

        var trimmedAtlas = Image.CreateEmpty(
            composition.Atlas.GetWidth(),
            finalHeight,
            false,
            Image.Format.Rgba8);
        trimmedAtlas.BlitRect(
            composition.Atlas,
            new Rect2I(0, 0, composition.Atlas.GetWidth(), finalHeight),
            Vector2I.Zero);
        return trimmedAtlas;
    }

    internal static Image CompositeImages(Image baseImage, Image borderImage)
    {
        var width = baseImage.GetWidth();
        var height = baseImage.GetHeight();
        borderImage = ResizeBorderIfNeeded(borderImage, width, height);

        var result = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        result.BlitRect(baseImage, new Rect2I(0, 0, width, height), Vector2I.Zero);
        BlendBorder(result, borderImage, width, height);
        return result;
    }

    internal static Image ExtractTileRegion(
        Image sourceImage,
        int atlasX,
        int atlasY,
        int tileSize,
        float sourceScale)
    {
        var sourcePixelSize = (int)(tileSize / sourceScale);
        var sourceRect = new Rect2I(
            atlasX * sourcePixelSize,
            atlasY * sourcePixelSize,
            sourcePixelSize,
            sourcePixelSize);
        var extracted = Image.CreateEmpty(
            sourcePixelSize,
            sourcePixelSize,
            false,
            Image.Format.Rgba8);
        extracted.BlitRect(sourceImage, sourceRect, Vector2I.Zero);

        if (Math.Abs(sourceScale - 1.0f) > 0.001f)
            extracted.Resize(tileSize, tileSize, Image.Interpolation.Nearest);

        return extracted;
    }

    private static void RenderPackedTile(
        Image atlasImage,
        PackedTile packedTile,
        TileSet tileSet,
        Vector2I tileSize)
    {
        var sourceImage = GetSourceImage(tileSet, packedTile.Region.SourceId);
        if (sourceImage == null)
            return;

        var sourceScale = packedTile.Region.SourceScale;
        var sourcePixelSize = (int)(tileSize.X / sourceScale);
        var sourceRect = new Rect2I(
            packedTile.Region.AtlasX * sourcePixelSize,
            packedTile.Region.AtlasY * sourcePixelSize,
            packedTile.Region.Width * sourcePixelSize,
            packedTile.Region.Height * sourcePixelSize);
        var targetSize = new Vector2I(
            packedTile.Region.Width * tileSize.X,
            packedTile.Region.Height * tileSize.Y);
        var destination = new Vector2I(
            packedTile.AtlasX + TileAtlasCompilerConstants.TilePadding,
            packedTile.AtlasY + TileAtlasCompilerConstants.TilePadding);

        if (Math.Abs(sourceScale - 1.0f) > 0.001f)
        {
            var extractedTile = Image.CreateEmpty(
                sourceRect.Size.X,
                sourceRect.Size.Y,
                false,
                Image.Format.Rgba8);
            extractedTile.BlitRect(sourceImage, sourceRect, Vector2I.Zero);
            extractedTile.Resize(
                targetSize.X,
                targetSize.Y,
                Image.Interpolation.Nearest);
            atlasImage.BlitRect(
                extractedTile,
                new Rect2I(0, 0, targetSize.X, targetSize.Y),
                destination);
        }
        else
        {
            atlasImage.BlitRect(sourceImage, sourceRect, destination);
        }

        ExtendEdgesToPadding(atlasImage, destination, targetSize);
    }

    private static Image? GetSourceImage(TileSet tileSet, int sourceId)
    {
        var source = tileSet.GetSource(sourceId) as TileSetAtlasSource;
        if (source?.Texture == null)
        {
            GD.PrintErr($"[TileAtlasCompiler] Missing source {sourceId}");
            return null;
        }

        var sourceImage = source.Texture.GetImage();
        if (sourceImage == null)
        {
            GD.PrintErr($"[TileAtlasCompiler] Cannot get image from source {sourceId}");
            return null;
        }

        return sourceImage;
    }

    private static void ExtendEdgesToPadding(Image atlas, Vector2I tilePos, Vector2I tileSize)
    {
        var padding = TileAtlasCompilerConstants.TilePadding;
        ExtendHorizontalEdge(atlas, tilePos, tileSize, padding, true);
        ExtendHorizontalEdge(atlas, tilePos, tileSize, padding, false);
        ExtendVerticalEdge(atlas, tilePos, tileSize, padding, true);
        ExtendVerticalEdge(atlas, tilePos, tileSize, padding, false);
    }

    private static void ExtendHorizontalEdge(
        Image atlas,
        Vector2I tilePos,
        Vector2I tileSize,
        int padding,
        bool top)
    {
        var edgeY = top ? tilePos.Y : tilePos.Y + tileSize.Y - 1;
        for (var x = tilePos.X; x < tilePos.X + tileSize.X; x++)
        {
            var edgeColor = atlas.GetPixel(x, edgeY);
            for (var distance = 1; distance <= padding; distance++)
            {
                var targetY = top ? edgeY - distance : edgeY + distance;
                if (targetY >= 0 && targetY < atlas.GetHeight())
                    atlas.SetPixel(x, targetY, edgeColor);
            }
        }
    }

    private static void ExtendVerticalEdge(
        Image atlas,
        Vector2I tilePos,
        Vector2I tileSize,
        int padding,
        bool left)
    {
        var edgeX = left ? tilePos.X : tilePos.X + tileSize.X - 1;
        for (var y = tilePos.Y; y < tilePos.Y + tileSize.Y; y++)
        {
            var edgeColor = atlas.GetPixel(edgeX, y);
            for (var distance = 1; distance <= padding; distance++)
            {
                var targetX = left ? edgeX - distance : edgeX + distance;
                if (targetX >= 0 && targetX < atlas.GetWidth())
                    atlas.SetPixel(targetX, y, edgeColor);
            }
        }
    }

    private static Image ResizeBorderIfNeeded(Image borderImage, int width, int height)
    {
        if (borderImage.GetWidth() == width && borderImage.GetHeight() == height)
            return borderImage;

        var resizedBorder = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        resizedBorder.BlitRect(
            borderImage,
            new Rect2I(0, 0, borderImage.GetWidth(), borderImage.GetHeight()),
            Vector2I.Zero);
        return resizedBorder;
    }

    private static void BlendBorder(Image result, Image borderImage, int width, int height)
    {
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var baseColor = result.GetPixel(x, y);
                var borderColor = borderImage.GetPixel(x, y);
                var alpha = borderColor.A;
                if (alpha <= 0)
                    continue;

                var blended = new Color(
                    borderColor.R * alpha + baseColor.R * (1 - alpha),
                    borderColor.G * alpha + baseColor.G * (1 - alpha),
                    borderColor.B * alpha + baseColor.B * (1 - alpha),
                    Math.Max(baseColor.A, alpha));
                result.SetPixel(x, y, blended);
            }
        }
    }
}
#endif
