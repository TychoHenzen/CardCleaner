#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Interactive dual-grid preview for exploring auto-tile configurations.
/// </summary>
[Tool]
public partial class DualGridAutoTilePreview : Control
{
    private const int DataGridCols = 15;
    private const int DataGridRows = 10;
    private const int VisualGridCols = DataGridCols + 1;
    private const int VisualGridRows = DataGridRows + 1;

    [Signal]
    public delegate void InfoChangedEventHandler(string info);

    private readonly TileEditorService? _service;
    private EditableTile? _overlayTile;
    private EditableTile? _baseTile;
    private float _scale = 2f;
    private Vector2I _tileSize = new(16, 16);
    private bool _showDataGrid = true;
    private string? _formatOverride;
    private readonly bool[,] _dataGrid = new bool[DataGridRows, DataGridCols];
    private int[,] _visualBitmasks = new int[VisualGridRows, VisualGridCols];

    public DualGridAutoTilePreview() { }

    public DualGridAutoTilePreview(TileEditorService service)
    {
        _service = service;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
        InitializeSamplePattern();
    }
}
#endif
