#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewControl
{
    private void DrawAutoTileBackground()
    {
        DrawRect(
            new Rect2(Vector2.Zero, CustomMinimumSize),
            new Color(0.15f, 0.15f, 0.15f, 1f));
    }

    private void DrawAutoTileBase(Vector2 scaledTileSize)
    {
        if (_baseTileDef == null || _baseTilesetRef == null)
            return;

        var baseTexture = GetTextureForTileset(_baseTilesetRef);
        if (baseTexture == null)
            return;

        var baseSrcRect = new Rect2(
            _baseTileDef.AtlasCoords.X * _tileSize.X,
            _baseTileDef.AtlasCoords.Y * _tileSize.Y,
            _tileSize.X,
            _tileSize.Y);
        for (var row = 0; row < DataGridRows + 1; row++)
        for (var col = 0; col < DataGridCols + 1; col++)
        {
            var destRect = new Rect2(
                col * scaledTileSize.X,
                row * scaledTileSize.Y,
                scaledTileSize.X,
                scaledTileSize.Y);
            DrawTextureRectRegion(baseTexture, destRect, baseSrcRect);
        }
    }

    private void DrawAutoTileVariants(Vector2 scaledTileSize)
    {
        var texture = GetAutoTileTexture();
        if (texture == null || _autoTileDef == null)
            return;

        if (GetEffectiveFormat() == "blob47")
            DrawBlobAutoTileVariants(texture, scaledTileSize);
        else
            DrawDualGridAutoTileVariants(texture, scaledTileSize);
    }

    private void DrawBlobAutoTileVariants(Texture2D texture, Vector2 scaledTileSize)
    {
        var halfTile = scaledTileSize / 2;
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            var bitmask = _visualBitmasks[row, col];
            if (bitmask < 0)
                continue;

            var atlasCoords = _autoTileDef!.GetAutoTileCoords(bitmask);
            var srcRect = GetTileSourceRect(atlasCoords);
            var destRect = new Rect2(
                col * scaledTileSize.X + halfTile.X,
                row * scaledTileSize.Y + halfTile.Y,
                scaledTileSize.X,
                scaledTileSize.Y);
            DrawTextureRectRegion(texture, destRect, srcRect);
        }
    }

    private void DrawDualGridAutoTileVariants(Texture2D texture, Vector2 scaledTileSize)
    {
        for (var row = 0; row < VisualGridRows; row++)
        for (var col = 0; col < VisualGridCols; col++)
        {
            var bitmask = _visualBitmasks[row, col];
            if (bitmask == 0)
                continue;

            var atlasCoords = _autoTileDef!.GetAutoTileCoords(bitmask);
            var srcRect = GetTileSourceRect(atlasCoords);
            var destRect = new Rect2(
                col * scaledTileSize.X,
                row * scaledTileSize.Y,
                scaledTileSize.X,
                scaledTileSize.Y);
            DrawTextureRectRegion(texture, destRect, srcRect);
        }
    }

    private Rect2 GetTileSourceRect(Vector2I atlasCoords)
    {
        return new Rect2(
            atlasCoords.X * _tileSize.X,
            atlasCoords.Y * _tileSize.Y,
            _tileSize.X,
            _tileSize.Y);
    }

    private void DrawAutoTileDataGrid(Vector2 scaledTileSize)
    {
        if (!_showDataGrid)
            return;

        var halfTile = scaledTileSize / 2;
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            var destRect = new Rect2(
                col * scaledTileSize.X + halfTile.X,
                row * scaledTileSize.Y + halfTile.Y,
                scaledTileSize.X,
                scaledTileSize.Y);
            var color = _dataGrid[row, col]
                ? new Color(0.2f, 0.8f, 0.2f, 0.25f)
                : new Color(0.8f, 0.2f, 0.2f, 0.1f);
            DrawRect(destRect, color);
            DrawRect(
                destRect,
                new Color(0.5f, 0.5f, 0.5f, 0.3f),
                false,
                1.0f);
        }
    }
}
#endif
