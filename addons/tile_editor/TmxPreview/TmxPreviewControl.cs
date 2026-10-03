#if TOOLS
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services.TilesetLoading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Control that renders tiles from a loaded TMX map or an interactive auto-tile preview.
/// </summary>
[Tool]
public partial class TmxPreviewControl : Control
{
    [Signal]
    public delegate void InfoChangedEventHandler(string info);

    private const int DataGridCols = 15;
    private const int DataGridRows = 10;
    private const int VisualGridCols = DataGridCols + 1;
    private const int VisualGridRows = DataGridRows + 1;

    private readonly TileEditorService? _service;
    private TmxMapData? _mapData;
    private float _scale = 2f;
    private Vector2I _tileSize = new(16, 16);
    private TileDefinition? _baseTileDef;
    private TmxTilesetReference? _baseTilesetRef;
    private TileDefinition? _autoTileDef;
    private TmxTilesetReference? _currentTilesetRef;
    private bool _isAutoTileMode;
    private bool _showDataGrid = true;
    private string? _formatOverride;
    private readonly bool[,] _dataGrid = new bool[DataGridRows, DataGridCols];
    private int[,] _visualBitmasks = new int[VisualGridRows, VisualGridCols];
    private readonly Dictionary<string, Texture2D?> _textureCache = new();

    public TmxPreviewControl() { }

    public TmxPreviewControl(TileEditorService service)
    {
        _service = service;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
    }
}
#endif
