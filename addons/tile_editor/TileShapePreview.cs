#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
///     A Control that draws a 3x3 grid visualization showing which parts of a tile
///     are "filled" based on an auto-tile bitmask. Works for both 4-bit corner and 8-bit blob formats.
/// </summary>
[Tool]
public partial class TileShapePreview : Control
{
    /// <summary>Auto-tile formats use 4-bit corners, 4-bit edges, or 8-bit edges and corners.</summary>
    public enum Format
    {
        /// <summary>4-bit corner format: NE=1, SE=2, SW=4, NW=8 (16 combinations)</summary>
        Corner16,

        /// <summary>8-bit blob format: N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128 (47 valid)</summary>
        Blob47,

        /// <summary>4-bit edge format: N=1, E=2, S=4, W=8 (16 combinations)</summary>
        Edge16
    }

    private int _bitmask;
    private Format _format = Format.Corner16;
    private Color _filledColor = new(0.4f, 0.7f, 0.4f);
    private Color _emptyColor = new(0.2f, 0.2f, 0.2f);
    private Color _centerColor = new(0.3f, 0.5f, 0.8f);
    private Color _borderColor = new(0.5f, 0.5f, 0.5f);

    /// <summary>The bitmask value to visualize (0-15 for Corner16, 0-255 for Blob47)</summary>
    public int Bitmask
    {
        get => _bitmask;
        set
        {
            if (_bitmask == value) return;
            _bitmask = value;
            QueueRedraw();
        }
    }

    /// <summary>The auto-tile format (determines how bitmask bits map to cells)</summary>
    public Format AutoTileFormat
    {
        get => _format;
        set
        {
            if (_format == value) return;
            _format = value;
            QueueRedraw();
        }
    }

    /// <summary>Color for filled neighbor cells</summary>
    public Color FilledColor
    {
        get => _filledColor;
        set
        {
            _filledColor = value;
            QueueRedraw();
        }
    }

    /// <summary>Color for empty neighbor cells</summary>
    public Color EmptyColor
    {
        get => _emptyColor;
        set
        {
            _emptyColor = value;
            QueueRedraw();
        }
    }

    /// <summary>Color for the center cell (the tile itself)</summary>
    public Color CenterColor
    {
        get => _centerColor;
        set
        {
            _centerColor = value;
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        var size = Size;
        var cellWidth = size.X / 3f;
        var cellHeight = size.Y / 3f;

        // Draw all 9 cells
        for (var row = 0; row < 3; row++)
        {
            for (var col = 0; col < 3; col++)
            {
                var rect = new Rect2(col * cellWidth, row * cellHeight, cellWidth, cellHeight);
                var color = GetCellColor(row, col);
                DrawRect(rect, color);
                DrawRect(rect, _borderColor, false, 1f);
            }
        }
    }

    private Color GetCellColor(int row, int col)
    {
        // Center cell is always the tile itself
        if (row == 1 && col == 1)
            return _centerColor;

        // Map grid position to direction and check bitmask
        var isFilled = _format switch
        {
            Format.Corner16 => IsCellFilledCorner16(row, col),
            Format.Edge16 => IsCellFilledEdge16(row, col),
            Format.Blob47 => IsCellFilledBlob47(row, col),
            _ => false
        };

        return isFilled ? _filledColor : _emptyColor;
    }

    /// <summary>
    ///     Check if a cell is filled for 4-bit corner format.
    ///     Only corners are controlled by bits; edges are empty.
    ///     NE=1 (bit 0), SE=2 (bit 1), SW=4 (bit 2), NW=8 (bit 3)
    /// </summary>
    private bool IsCellFilledCorner16(int row, int col)
    {
        // Grid layout (row, col):
        // (0,0)=NW  (0,1)=N   (0,2)=NE
        // (1,0)=W   (1,1)=C   (1,2)=E
        // (2,0)=SW  (2,1)=S   (2,2)=SE

        return (row, col) switch
        {
            (0, 2) => (_bitmask & 1) != 0,  // NE = bit 0
            (2, 2) => (_bitmask & 2) != 0,  // SE = bit 1
            (2, 0) => (_bitmask & 4) != 0,  // SW = bit 2
            (0, 0) => (_bitmask & 8) != 0,  // NW = bit 3
            _ => false // Edges (N, E, S, W) are not controlled in corner format
        };
    }

    /// <summary>
    ///     Check if a cell is filled for 4-bit edge format.
    ///     Only cardinal directions are controlled by bits; corners are empty.
    ///     N=1 (bit 0), E=2 (bit 1), S=4 (bit 2), W=8 (bit 3)
    /// </summary>
    private bool IsCellFilledEdge16(int row, int col)
    {
        // Grid layout (row, col):
        // (0,0)=NW  (0,1)=N   (0,2)=NE
        // (1,0)=W   (1,1)=C   (1,2)=E
        // (2,0)=SW  (2,1)=S   (2,2)=SE

        return (row, col) switch
        {
            (0, 1) => (_bitmask & 1) != 0,  // N = bit 0
            (1, 2) => (_bitmask & 2) != 0,  // E = bit 1
            (2, 1) => (_bitmask & 4) != 0,  // S = bit 2
            (1, 0) => (_bitmask & 8) != 0,  // W = bit 3
            _ => false // Corners (NE, SE, SW, NW) are not controlled in edge format
        };
    }

    /// <summary>
    ///     Check if a cell is filled for 8-bit blob format.
    ///     All 8 surrounding cells are controlled by bits.
    ///     N=1, NE=2, E=4, SE=8, S=16, SW=32, W=64, NW=128
    /// </summary>
    private bool IsCellFilledBlob47(int row, int col)
    {
        // Grid layout (row, col):
        // (0,0)=NW  (0,1)=N   (0,2)=NE
        // (1,0)=W   (1,1)=C   (1,2)=E
        // (2,0)=SW  (2,1)=S   (2,2)=SE

        return (row, col) switch
        {
            (0, 1) => (_bitmask & 1) != 0,    // N  = bit 0
            (0, 2) => (_bitmask & 2) != 0,    // NE = bit 1
            (1, 2) => (_bitmask & 4) != 0,    // E  = bit 2
            (2, 2) => (_bitmask & 8) != 0,    // SE = bit 3
            (2, 1) => (_bitmask & 16) != 0,   // S  = bit 4
            (2, 0) => (_bitmask & 32) != 0,   // SW = bit 5
            (1, 0) => (_bitmask & 64) != 0,   // W  = bit 6
            (0, 0) => (_bitmask & 128) != 0,  // NW = bit 7
            _ => false
        };
    }

    /// <summary>
    ///     Set the bitmask and format together (useful for batch updates).
    /// </summary>
    public void SetMask(int bitmask, Format format)
    {
        _bitmask = bitmask;
        _format = format;
        QueueRedraw();
    }
}
#endif
