#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class DualGridAutoTilePreview
{
    public override void _Draw()
    {
        if (_service == null || _overlayTile == null)
            return;

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var halfTile = scaledTileSize / 2;
        DrawBaseTiles(GetBaseTexture(), scaledTileSize);
        DrawOverlayTiles(_service.GetTileTexture(_overlayTile), scaledTileSize, halfTile);
        if (_showDataGrid)
            DrawDataGrid(scaledTileSize, halfTile);
        DrawGridBoundary(scaledTileSize);
    }

    private Texture2D? GetBaseTexture()
    {
        return _baseTile == null ? null : _service!.GetTileTexture(_baseTile);
    }

    private void DrawBaseTiles(Texture2D? texture, Vector2 scaledTileSize)
    {
        if (texture == null || _baseTile == null)
            return;

        var actualTileSize = GetActualTileSize(_baseTile.SourceScale);
        var baseCoords = new Vector2I(_baseTile.AtlasX, _baseTile.AtlasY);
        var sourceRect = new Rect2(
            baseCoords.X * actualTileSize.X,
            baseCoords.Y * actualTileSize.Y,
            actualTileSize.X,
            actualTileSize.Y);
        for (var row = 0; row < DataGridRows + 1; row++)
        for (var col = 0; col < DataGridCols + 1; col++)
        {
            var destination = new Rect2(
                col * scaledTileSize.X,
                row * scaledTileSize.Y,
                scaledTileSize.X,
                scaledTileSize.Y);
            DrawTextureRectRegion(texture, destination, sourceRect);
        }
    }

    private void DrawOverlayTiles(
        Texture2D? texture,
        Vector2 scaledTileSize,
        Vector2 halfTile)
    {
        if (texture == null)
            return;

        var actualTileSize = GetActualTileSize(_overlayTile!.SourceScale);
        if (GetEffectiveFormat() == "blob47")
            DrawBlobOverlay(texture, actualTileSize, scaledTileSize, halfTile);
        else
            DrawDualGridOverlay(texture, actualTileSize, scaledTileSize);
    }

    private void DrawBlobOverlay(
        Texture2D texture,
        Vector2I actualTileSize,
        Vector2 scaledTileSize,
        Vector2 halfTile)
    {
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            var bitmask = _visualBitmasks[row, col];
            if (bitmask < 0)
                continue;

            var variantInfo = GetVariantInfo(bitmask);
            var destination = new Rect2(
                col * scaledTileSize.X + halfTile.X,
                row * scaledTileSize.Y + halfTile.Y,
                scaledTileSize.X * variantInfo.Size.X,
                scaledTileSize.Y * variantInfo.Size.Y);
            DrawVariant(texture, destination, actualTileSize, variantInfo);
        }
    }

    private void DrawDualGridOverlay(
        Texture2D texture,
        Vector2I actualTileSize,
        Vector2 scaledTileSize)
    {
        for (var row = 0; row < VisualGridRows; row++)
        for (var col = 0; col < VisualGridCols; col++)
        {
            var bitmask = _visualBitmasks[row, col];
            if (bitmask == 0)
                continue;

            var variantInfo = GetVariantInfo(bitmask);
            var destination = new Rect2(
                col * scaledTileSize.X,
                row * scaledTileSize.Y,
                scaledTileSize.X * variantInfo.Size.X,
                scaledTileSize.Y * variantInfo.Size.Y);
            DrawVariant(texture, destination, actualTileSize, variantInfo);
        }
    }

    private void DrawVariant(
        Texture2D texture,
        Rect2 destination,
        Vector2I actualTileSize,
        VariantPreviewInfo variantInfo)
    {
        var sourceRect = new Rect2(
            variantInfo.AtlasCoords.X * actualTileSize.X,
            variantInfo.AtlasCoords.Y * actualTileSize.Y,
            actualTileSize.X * variantInfo.Size.X,
            actualTileSize.Y * variantInfo.Size.Y);
        DrawTextureRectRegion(texture, destination, sourceRect);
    }

    private Vector2I GetActualTileSize(float sourceScale)
    {
        return new Vector2I(
            (int)(_tileSize.X / sourceScale),
            (int)(_tileSize.Y / sourceScale));
    }

    private void DrawDataGrid(Vector2 scaledTileSize, Vector2 halfTile)
    {
        for (var row = 0; row < DataGridRows; row++)
        for (var col = 0; col < DataGridCols; col++)
        {
            var destination = new Rect2(
                col * scaledTileSize.X + halfTile.X,
                row * scaledTileSize.Y + halfTile.Y,
                scaledTileSize.X,
                scaledTileSize.Y);
            var color = _dataGrid[row, col]
                ? new Color(0.2f, 0.8f, 0.2f, 0.25f)
                : new Color(0.8f, 0.2f, 0.2f, 0.1f);
            DrawRect(destination, color);
            DrawRect(destination, new Color(0.5f, 0.5f, 0.5f, 0.3f), false, 1.0f);
        }
    }

    private void DrawGridBoundary(Vector2 scaledTileSize)
    {
        var totalSize = new Vector2(
            (DataGridCols + 1) * scaledTileSize.X,
            (DataGridRows + 1) * scaledTileSize.Y);
        DrawRect(
            new Rect2(Vector2.Zero, totalSize),
            new Color(0.7f, 0.7f, 0.7f, 0.5f),
            false,
            2.0f);
    }
}
#endif
