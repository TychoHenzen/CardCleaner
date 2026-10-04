#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Geometry of an atlas texture drawn at a fixed scale: tile size on screen and the column and
/// row counts, plus the conversions between mouse positions and tile cells.
/// </summary>
internal readonly struct AtlasGridLayout
{
    internal AtlasGridLayout(Vector2 textureSize, Vector2I sourceTileSize, float scale)
    {
        ScaledSize = textureSize * scale;
        ScaledTile = new Vector2(sourceTileSize.X, sourceTileSize.Y) * scale;
        Cols = (int)(textureSize.X / sourceTileSize.X);
        Rows = (int)(textureSize.Y / sourceTileSize.Y);
    }

    internal Vector2 ScaledSize { get; }

    internal Vector2 ScaledTile { get; }

    internal int Cols { get; }

    internal int Rows { get; }

    /// <summary>
    /// True when the cell lies inside the atlas grid.
    /// </summary>
    internal bool Contains(Vector2I cell)
    {
        return cell.X >= 0 && cell.Y >= 0 && cell.X < Cols && cell.Y < Rows;
    }

    /// <summary>
    /// Gets the on-screen rectangle covering a block of cells starting at <paramref name="origin"/>.
    /// </summary>
    internal Rect2 CellRect(Vector2I origin, Vector2I size)
    {
        return new Rect2(
            origin.X * ScaledTile.X,
            origin.Y * ScaledTile.Y,
            ScaledTile.X * size.X,
            ScaledTile.Y * size.Y);
    }

    /// <summary>
    /// Maps a position to its cell, or (-1, -1) when the position is outside the grid.
    /// </summary>
    internal Vector2I CellAt(Vector2 position)
    {
        var x = (int)(position.X / ScaledTile.X);
        var y = (int)(position.Y / ScaledTile.Y);

        var cell = new Vector2I(x, y);
        return Contains(cell) ? cell : new Vector2I(-1, -1);
    }
}
#endif
