#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewControl
{
    public override void _Draw()
    {
        if (_service == null)
            return;

        if (_isAutoTileMode && _autoTileDef != null)
        {
            DrawAutoTileMode();
            return;
        }

        if (_mapData != null)
            DrawTmxMapMode();
    }

    private void DrawAutoTileMode()
    {
        var scaledTileSize = GetScaledTileSize();
        DrawAutoTileBackground();
        DrawAutoTileBase(scaledTileSize);
        DrawAutoTileVariants(scaledTileSize);
        DrawAutoTileDataGrid(scaledTileSize);
        DrawControlBorder();
    }

    private void DrawTmxMapMode()
    {
        if (_mapData == null)
            return;

        var scaledTileSize = GetScaledTileSize();
        DrawMapBackground();
        DrawMapTiles(scaledTileSize);
        DrawMapGrid(scaledTileSize);
        DrawControlBorder();
    }

    private void DrawControlBorder()
    {
        DrawRect(
            new Rect2(Vector2.Zero, CustomMinimumSize),
            new Color(0.7f, 0.7f, 0.7f, 0.5f),
            false,
            2.0f);
    }
}
#endif
