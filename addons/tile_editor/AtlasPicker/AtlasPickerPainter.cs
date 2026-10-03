#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Draws the hover highlight, selection highlight and grid lines of the atlas picker
/// onto the canvas item that is currently handling its draw notification.
/// </summary>
internal static class AtlasPickerPainter
{
    private static readonly Color GridColor = new(0.3f, 0.3f, 0.3f, 0.8f);
    private static readonly Color SelectionColor = new(1f, 0.8f, 0f, 0.8f);
    private static readonly Color HoverColor = new(1f, 1f, 1f, 0.4f);

    /// <summary>
    /// Draws the hover highlight (sized by the selection block for multi-tile preview).
    /// </summary>
    internal static void DrawHover(CanvasItem canvas, AtlasGridLayout layout, AtlasPickerSelection selection)
    {
        if (!layout.Contains(selection.Hovered))
            return;

        canvas.DrawRect(layout.CellRect(selection.Hovered, selection.Size), HoverColor);

        // Draw grid lines within multi-tile hover area
        if (selection.Size.X > 1 || selection.Size.Y > 1)
            DrawBlockDividers(canvas, layout, selection);
    }

    /// <summary>
    /// Draws the selection highlight (sized by the selection block for multi-tile).
    /// </summary>
    internal static void DrawSelection(CanvasItem canvas, AtlasGridLayout layout, AtlasPickerSelection selection)
    {
        if (!layout.Contains(selection.Selected))
            return;

        var selectRect = layout.CellRect(selection.Selected, selection.Size);
        // Draw selection as filled rectangle with transparency
        canvas.DrawRect(selectRect, SelectionColor with { A = 0.3f });
        // Draw selection outline
        canvas.DrawRect(selectRect, SelectionColor, false, 3.0f);
    }

    /// <summary>
    /// Draws the vertical and horizontal grid lines over the whole atlas.
    /// </summary>
    internal static void DrawGridLines(CanvasItem canvas, AtlasGridLayout layout)
    {
        for (var x = 0; x <= layout.Cols; x++)
        {
            var xPos = x * layout.ScaledTile.X;
            canvas.DrawLine(new Vector2(xPos, 0), new Vector2(xPos, layout.ScaledSize.Y), GridColor);
        }

        for (var y = 0; y <= layout.Rows; y++)
        {
            var yPos = y * layout.ScaledTile.Y;
            canvas.DrawLine(new Vector2(0, yPos), new Vector2(layout.ScaledSize.X, yPos), GridColor);
        }
    }

    private static void DrawBlockDividers(
        CanvasItem canvas,
        AtlasGridLayout layout,
        AtlasPickerSelection selection)
    {
        var origin = selection.Hovered;
        var tile = layout.ScaledTile;
        var dividerColor = HoverColor * 1.5f;

        for (var dx = 1; dx < selection.Size.X; dx++)
        {
            var xPos = (origin.X + dx) * tile.X;
            canvas.DrawLine(
                new Vector2(xPos, origin.Y * tile.Y),
                new Vector2(xPos, (origin.Y + selection.Size.Y) * tile.Y),
                dividerColor);
        }

        for (var dy = 1; dy < selection.Size.Y; dy++)
        {
            var yPos = (origin.Y + dy) * tile.Y;
            canvas.DrawLine(
                new Vector2(origin.X * tile.X, yPos),
                new Vector2((origin.X + selection.Size.X) * tile.X, yPos),
                dividerColor);
        }
    }
}
#endif
