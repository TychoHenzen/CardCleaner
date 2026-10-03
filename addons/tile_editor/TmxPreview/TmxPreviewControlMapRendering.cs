#if TOOLS
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewControl
{
    private void DrawMapBackground()
    {
        DrawRect(
            new Rect2(Vector2.Zero, CustomMinimumSize),
            new Color(0.15f, 0.15f, 0.15f, 1f));
    }

    private void DrawMapTiles(Vector2 scaledTileSize)
    {
        var bounds = _mapData!.GetBounds();
        var baseTexture = GetBaseMapTexture();
        var baseSrcRect = baseTexture == null ? null : GetBaseMapSourceRect();
        foreach (var (coord, resolution) in _mapData.GetAllTiles())
            DrawMapTile(coord, resolution, bounds, scaledTileSize, baseTexture, baseSrcRect);
    }

    private Texture2D? GetBaseMapTexture()
    {
        return _baseTilesetRef == null || _baseTileDef == null
            ? null
            : GetTextureForTileset(_baseTilesetRef);
    }

    private Rect2? GetBaseMapSourceRect()
    {
        if (_baseTileDef == null)
            return null;

        return GetTileSourceRect(_baseTileDef.AtlasCoords);
    }

    private void DrawMapTile(
        Vector2I coord,
        TmxTileResolution resolution,
        TmxBounds bounds,
        Vector2 scaledTileSize,
        Texture2D? baseTexture,
        Rect2? baseSrcRect)
    {
        var texture = GetTextureForTileset(resolution.Tileset);
        if (texture == null)
            return;

        var destRect = new Rect2(
            (coord.X - bounds.Min.X) * scaledTileSize.X,
            (coord.Y - bounds.Min.Y) * scaledTileSize.Y,
            scaledTileSize.X,
            scaledTileSize.Y);
        if (IsTransparentTile(resolution.TileDefinition))
            DrawTransparentTileBase(coord, destRect, baseTexture, baseSrcRect);

        DrawTextureRectRegion(
            texture,
            destRect,
            GetTileSourceRect(resolution.AtlasCoords));
    }

    private static bool IsTransparentTile(TileDefinition? tileDefinition)
    {
        return tileDefinition?.IsTransparent == true
            || tileDefinition?.IsCompositable == true
            || tileDefinition?.HasAutoTileVariants == true;
    }

    private void DrawTransparentTileBase(
        Vector2I coord,
        Rect2 destRect,
        Texture2D? baseTexture,
        Rect2? baseSrcRect)
    {
        if (baseTexture != null && baseSrcRect.HasValue)
            DrawTextureRectRegion(baseTexture, destRect, baseSrcRect.Value);
        else
            DrawCheckerboard(destRect, coord);
    }

    private void DrawMapGrid(Vector2 scaledTileSize)
    {
        var bounds = _mapData!.GetBounds();
        var gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        var width = bounds.Max.X - bounds.Min.X + 1;
        var height = bounds.Max.Y - bounds.Min.Y + 1;
        for (var x = 0; x <= width; x++)
        {
            DrawLine(
                new Vector2(x * scaledTileSize.X, 0),
                new Vector2(x * scaledTileSize.X, height * scaledTileSize.Y),
                gridColor);
        }

        for (var y = 0; y <= height; y++)
        {
            DrawLine(
                new Vector2(0, y * scaledTileSize.Y),
                new Vector2(width * scaledTileSize.X, y * scaledTileSize.Y),
                gridColor);
        }
    }

    private void DrawCheckerboard(Rect2 destRect, Vector2I coord)
    {
        var lightColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        var darkColor = new Color(0.25f, 0.25f, 0.25f, 1f);
        var checkerSize = destRect.Size / 4;
        for (var cy = 0; cy < 4; cy++)
        for (var cx = 0; cx < 4; cx++)
        {
            var isLight = ((coord.X + coord.Y + cx + cy) % 2) == 0;
            var checkerRect = new Rect2(
                destRect.Position.X + cx * checkerSize.X,
                destRect.Position.Y + cy * checkerSize.Y,
                checkerSize.X,
                checkerSize.Y);
            DrawRect(checkerRect, isLight ? lightColor : darkColor);
        }
    }
}
#endif
