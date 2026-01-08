#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel showing a preview of tiles as they resolve in an actual TMX file.
/// Loads a TMX file and displays the tiles exactly as Tiled would render them,
/// allowing visual verification that auto-tile configurations work correctly.
/// </summary>
[Tool]
public partial class TmxPreviewPanel : VBoxContainer
{
    private const string DefaultTmxDir = "res://Data/Tiled";

    private readonly TileEditorService? _service;
    private LineEdit? _tmxPathEdit;
    private Button? _browseButton;
    private Button? _reloadButton;
    private OptionButton? _baseTileSelector;
    private OptionButton? _autoTileSelector;
    private OptionButton? _formatOverrideDropdown;
    private CheckBox? _showDataGridCheckbox;
    private HSlider? _scaleSlider;
    private Label? _scaleLabel;
    private Label? _infoLabel;
    private TmxPreviewControl? _previewControl;
    private string? _currentTmxPath;
    private TileDefinition? _selectedBaseTile;
    private TiledTilesetLoader.TmxTilesetReference? _selectedBaseTileTileset;
    private TileDefinition? _selectedAutoTile;
    private TiledTilesetLoader.TmxTilesetReference? _selectedAutoTileTileset;
    private string? _formatOverride;
    private TiledTilesetLoader.TmxMapData? _currentMapData;
    private List<(TileDefinition Tile, TiledTilesetLoader.TmxTilesetReference Tileset)> _availableAutoTiles = new();
    private List<(TileDefinition Tile, TiledTilesetLoader.TmxTilesetReference Tileset)> _availableBaseTiles = new();

    // Required by Godot for [Tool] classes
    public TmxPreviewPanel() { }

    public TmxPreviewPanel(TileEditorService service)
    {
        _service = service;
    }

    public override void _Ready()
    {
        if (_service == null) return;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        // Header
        var header = new Label
        {
            Text = "TMX Preview",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 16);
        AddChild(header);

        AddChild(new HSeparator());

        // TMX file path row
        var pathRow = new HBoxContainer();
        pathRow.AddChild(new Label { Text = "TMX File:", CustomMinimumSize = new Vector2(60, 0) });

        _tmxPathEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "Select a TMX file...",
            Editable = false
        };
        pathRow.AddChild(_tmxPathEdit);

        _browseButton = new Button { Text = "Browse..." };
        _browseButton.Pressed += OnBrowsePressed;
        pathRow.AddChild(_browseButton);

        _reloadButton = new Button { Text = "⟳", TooltipText = "Reload", Disabled = true };
        _reloadButton.Pressed += OnReloadPressed;
        pathRow.AddChild(_reloadButton);

        AddChild(pathRow);

        // Auto-tile selector row (for interactive dual-grid preview)
        var autoTileRow = new HBoxContainer();
        autoTileRow.AddChild(new Label { Text = "Auto-tile:", CustomMinimumSize = new Vector2(60, 0) });
        _autoTileSelector = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Select an auto-tile from the TMX to preview its variants interactively"
        };
        _autoTileSelector.ItemSelected += OnAutoTileSelected;
        autoTileRow.AddChild(_autoTileSelector);
        AddChild(autoTileRow);

        // Base tile selector row (for compositing transparent tiles)
        var baseRow = new HBoxContainer();
        baseRow.AddChild(new Label { Text = "Base tile:", CustomMinimumSize = new Vector2(60, 0) });
        _baseTileSelector = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Base terrain for compositing transparent Wang tiles"
        };
        _baseTileSelector.ItemSelected += OnBaseTileSelected;
        baseRow.AddChild(_baseTileSelector);
        AddChild(baseRow);
        PopulateBaseTileSelector();

        // Format override selector row
        var formatRow = new HBoxContainer();
        formatRow.AddChild(new Label { Text = "Format:", CustomMinimumSize = new Vector2(60, 0) });
        _formatOverrideDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Override format for auto-tile preview (uses tile's format if not set)"
        };
        PopulateFormatDropdown();
        _formatOverrideDropdown.ItemSelected += OnFormatOverrideSelected;
        formatRow.AddChild(_formatOverrideDropdown);
        AddChild(formatRow);

        // Scale slider row
        var scaleRow = new HBoxContainer();
        scaleRow.AddChild(new Label { Text = "Scale:", CustomMinimumSize = new Vector2(60, 0) });
        _scaleSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 4,
            Step = 0.5,
            Value = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scaleSlider.ValueChanged += OnScaleChanged;
        scaleRow.AddChild(_scaleSlider);
        _scaleLabel = new Label { Text = "2x", CustomMinimumSize = new Vector2(30, 0) };
        scaleRow.AddChild(_scaleLabel);
        AddChild(scaleRow);

        // Show data grid checkbox
        var optionsRow = new HBoxContainer();
        _showDataGridCheckbox = new CheckBox { Text = "Show data grid overlay", ButtonPressed = true };
        _showDataGridCheckbox.Toggled += OnShowDataGridToggled;
        optionsRow.AddChild(_showDataGridCheckbox);
        AddChild(optionsRow);

        AddChild(new HSeparator());

        // Info label
        _infoLabel = new Label
        {
            Text = "Select a TMX file to preview how tiles resolve.",
            Modulate = new Color(0.8f, 0.8f, 0.8f),
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(_infoLabel);

        // Preview in scroll container
        var previewScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };

        _previewControl = new TmxPreviewControl(_service);
        _previewControl.InfoChanged += OnInfoChanged;
        previewScroll.AddChild(_previewControl);
        AddChild(previewScroll);

        // Auto-load first TMX file if available
        CallDeferred(MethodName.TryAutoLoadTmx);
    }

    private void TryAutoLoadTmx()
    {
        var absoluteDir = ProjectSettings.GlobalizePath(DefaultTmxDir);
        if (!Directory.Exists(absoluteDir)) return;

        var tmxFiles = Directory.GetFiles(absoluteDir, "*.tmx");
        if (tmxFiles.Length > 0)
        {
            LoadTmxFile(tmxFiles[0]);
        }
    }

    private void OnBrowsePressed()
    {
        var dialog = new FileDialog
        {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            Filters = new[] { "*.tmx ; Tiled Map Files" },
            Title = "Select TMX File"
        };

        // Set initial directory
        var absoluteDir = ProjectSettings.GlobalizePath(DefaultTmxDir);
        if (Directory.Exists(absoluteDir))
        {
            dialog.CurrentDir = absoluteDir;
        }

        dialog.FileSelected += path =>
        {
            LoadTmxFile(path);
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();

        AddChild(dialog);
        dialog.PopupCentered(new Vector2I(600, 400));
    }

    private void OnReloadPressed()
    {
        if (!string.IsNullOrEmpty(_currentTmxPath))
        {
            LoadTmxFile(_currentTmxPath);
        }
    }

    private void LoadTmxFile(string path)
    {
        _currentTmxPath = path;
        _tmxPathEdit!.Text = Path.GetFileName(path);
        _reloadButton!.Disabled = false;

        var mapData = TiledTilesetLoader.LoadTmxMap(path);
        if (mapData == null)
        {
            _infoLabel!.Text = $"Failed to load TMX file: {path}";
            _previewControl?.ClearPreview();
            _currentMapData = null;
            PopulateAutoTileSelector();
            return;
        }

        _currentMapData = mapData;
        PopulateAutoTileSelector();
        PopulateBaseTileSelector();

        // Clear selections when loading new TMX
        _selectedAutoTile = null;
        _selectedAutoTileTileset = null;
        _selectedBaseTile = null;
        _selectedBaseTileTileset = null;
        _autoTileSelector!.Select(0);
        _baseTileSelector!.Select(0);

        _previewControl?.LoadTmxMap(mapData, (float)_scaleSlider!.Value);
    }

    private void OnScaleChanged(double value)
    {
        _scaleLabel!.Text = $"{value:F1}x";
        _previewControl?.SetScale((float)value);
    }

    private void OnShowDataGridToggled(bool pressed)
    {
        _previewControl?.SetShowDataGrid(pressed);
    }

    private void OnInfoChanged(string info)
    {
        if (_infoLabel != null)
            _infoLabel.Text = info;
    }

    private void PopulateBaseTileSelector()
    {
        if (_baseTileSelector == null) return;

        _baseTileSelector.Clear();
        _availableBaseTiles.Clear();
        _baseTileSelector.AddItem("-- None (checkerboard) --", 0);

        if (_currentMapData == null) return;

        var index = 1;
        foreach (var tilesetRef in _currentMapData.Tilesets)
        {
            var tilesetName = Path.GetFileNameWithoutExtension(tilesetRef.TsxPath);
            foreach (var tileDef in tilesetRef.TilesetData.Tiles)
            {
                // Simple terrains only (no auto-tile variants)
                if (!tileDef.HasAutoTileVariants)
                {
                    _availableBaseTiles.Add((tileDef, tilesetRef));
                    var displayText = $"{tileDef.Name} ({tileDef.Id}) [{tilesetName}]";
                    _baseTileSelector.AddItem(displayText, index);
                    _baseTileSelector.SetItemMetadata(index, index - 1); // Store list index
                    index++;
                }
            }
        }
    }

    private void OnBaseTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedBaseTile = null;
            _selectedBaseTileTileset = null;
        }
        else
        {
            var listIndex = _baseTileSelector!.GetItemMetadata((int)index).AsInt32();
            if (listIndex >= 0 && listIndex < _availableBaseTiles.Count)
            {
                var (tile, tileset) = _availableBaseTiles[listIndex];
                _selectedBaseTile = tile;
                _selectedBaseTileTileset = tileset;
            }
        }

        _previewControl?.SetBaseTile(_selectedBaseTile, _selectedBaseTileTileset);
    }

    private void PopulateFormatDropdown()
    {
        if (_formatOverrideDropdown == null || _service == null) return;

        _formatOverrideDropdown.Clear();
        _formatOverrideDropdown.AddItem("(Use tile's format)", 0);
        _formatOverrideDropdown.SetItemMetadata(0, "");

        var formatNames = _service.GetAvailableFormatNames();
        var index = 1;
        foreach (var formatName in formatNames)
        {
            var displayName = _service.GetFormatDisplayName(formatName);
            _formatOverrideDropdown.AddItem(displayName, index);
            _formatOverrideDropdown.SetItemMetadata(index, formatName);
            index++;
        }
    }

    private void OnFormatOverrideSelected(long index)
    {
        if (_formatOverrideDropdown == null) return;

        var metadata = _formatOverrideDropdown.GetItemMetadata((int)index).AsString();
        _formatOverride = string.IsNullOrEmpty(metadata) ? null : metadata;
        RefreshAutoTilePreview();
    }

    private void RefreshAutoTilePreview()
    {
        if (_selectedAutoTile != null && _selectedAutoTileTileset != null)
        {
            _previewControl?.SetAutoTilePreview(_selectedAutoTile, _selectedAutoTileTileset, (float)_scaleSlider!.Value, _formatOverride);
        }
    }

    private void PopulateAutoTileSelector()
    {
        if (_autoTileSelector == null) return;

        _autoTileSelector.Clear();
        _availableAutoTiles.Clear();
        _autoTileSelector.AddItem("-- TMX View (no auto-tile) --", 0);

        if (_currentMapData == null) return;

        var index = 1;
        foreach (var tilesetRef in _currentMapData.Tilesets)
        {
            var tilesetName = Path.GetFileNameWithoutExtension(tilesetRef.TsxPath);
            foreach (var tileDef in tilesetRef.TilesetData.Tiles)
            {
                if (tileDef.HasAutoTileVariants)
                {
                    _availableAutoTiles.Add((tileDef, tilesetRef));
                    var displayText = $"{tileDef.Name} ({tileDef.Id}) [{tilesetName}]";
                    _autoTileSelector.AddItem(displayText, index);
                    _autoTileSelector.SetItemMetadata(index, index - 1); // Store list index
                    index++;
                }
            }
        }
    }

    private void OnAutoTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedAutoTile = null;
            _selectedAutoTileTileset = null;
            // Switch back to TMX map view
            if (_currentMapData != null)
            {
                _previewControl?.LoadTmxMap(_currentMapData, (float)_scaleSlider!.Value);
            }
            return;
        }

        var listIndex = _autoTileSelector!.GetItemMetadata((int)index).AsInt32();
        if (listIndex >= 0 && listIndex < _availableAutoTiles.Count)
        {
            var (tile, tileset) = _availableAutoTiles[listIndex];
            _selectedAutoTile = tile;
            _selectedAutoTileTileset = tileset;
            _previewControl?.SetAutoTilePreview(tile, tileset, (float)_scaleSlider!.Value, _formatOverride);
        }
    }

    public void Refresh()
    {
        if (!string.IsNullOrEmpty(_currentTmxPath))
        {
            LoadTmxFile(_currentTmxPath);
        }
        PopulateBaseTileSelector();
        PopulateAutoTileSelector();
    }
}

/// <summary>
/// Control that renders tiles from a loaded TMX map.
/// Displays the actual tile resolution from the TMX data.
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
    private TiledTilesetLoader.TmxMapData? _mapData;
    private float _scale = 2f;
    private Vector2I _tileSize = new(16, 16);

    // Base tile for compositing (from TSX)
    private TileDefinition? _baseTileDef;
    private TiledTilesetLoader.TmxTilesetReference? _baseTilesetRef;

    // Auto-tile preview mode fields
    private TileDefinition? _autoTileDef;
    private TiledTilesetLoader.TmxTilesetReference? _currentTilesetRef;
    private bool _isAutoTileMode;
    private bool _showDataGrid = true;
    private string? _formatOverride;
    private readonly bool[,] _dataGrid = new bool[DataGridRows, DataGridCols];
    private int[,] _visualBitmasks = new int[VisualGridRows, VisualGridCols];

    // Texture cache for TSX-referenced textures (keyed by resolved texture path)
    private readonly Dictionary<string, Texture2D?> _textureCache = new();

    // Required by Godot for [Tool] classes
    public TmxPreviewControl() { }

    public TmxPreviewControl(TileEditorService service)
    {
        _service = service;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public void SetBaseTile(TileDefinition? baseTile, TiledTilesetLoader.TmxTilesetReference? tilesetRef)
    {
        _baseTileDef = baseTile;
        _baseTilesetRef = tilesetRef;
        QueueRedraw();
    }

    public void LoadTmxMap(TiledTilesetLoader.TmxMapData mapData, float scale)
    {
        _mapData = mapData;
        _scale = scale;
        // Get tile size from the first tileset's config (TSX tile dimensions)
        _tileSize = mapData.Tilesets.Count > 0
            ? mapData.Tilesets[0].TilesetData.TilesetConfig.BaseTileSize
            : new Vector2I(16, 16);
        _isAutoTileMode = false;
        _autoTileDef = null;
        _currentTilesetRef = null;

        GD.Print($"[TmxPreviewControl] LoadTmxMap: {mapData.Tilesets.Count} tilesets, tileSize={_tileSize}");
        foreach (var ts in mapData.Tilesets)
        {
            GD.Print($"[TmxPreviewControl]   Tileset: {ts.TsxPath}, TilesetPath='{ts.TilesetData.TilesetPath}', Tiles={ts.TilesetData.Tiles.Count}");
        }

        UpdateSize();
        QueueRedraw();
        EmitInfo();
    }

    public void SetScale(float scale)
    {
        _scale = scale;
        UpdateSize();
        QueueRedraw();
    }

    public void ClearPreview()
    {
        _mapData = null;
        _autoTileDef = null;
        _isAutoTileMode = false;
        UpdateSize();
        QueueRedraw();
        EmitSignal(SignalName.InfoChanged, "No TMX file loaded.");
    }

    /// <summary>
    /// Switch to interactive auto-tile preview mode for the given tile definition.
    /// </summary>
    public void SetAutoTilePreview(TileDefinition tileDef, TiledTilesetLoader.TmxTilesetReference tilesetRef, float scale, string? formatOverride = null)
    {
        _autoTileDef = tileDef;
        _currentTilesetRef = tilesetRef;
        _isAutoTileMode = true;
        _scale = scale;
        _formatOverride = formatOverride;
        // Get tile size from the tileset's config (TSX tile dimensions)
        _tileSize = tilesetRef.TilesetData.TilesetConfig.BaseTileSize;
        InitializeDataGrid();
        RecomputeVisualBitmasks();
        UpdateSize();
        QueueRedraw();
        EmitAutoTileInfo();
    }

    private string GetEffectiveFormat()
    {
        if (!string.IsNullOrEmpty(_formatOverride))
            return _formatOverride;
        return _autoTileDef?.AutoTileFormatName?.ToLowerInvariant() ?? "corner16";
    }

    public void SetShowDataGrid(bool show)
    {
        _showDataGrid = show;
        QueueRedraw();
    }

    private void InitializeDataGrid()
    {
        // Clear and create sample pattern
        Array.Clear(_dataGrid);

        // Create rectangular blob in center
        for (var row = 3; row <= 6; row++)
        for (var col = 5; col <= 9; col++)
            _dataGrid[row, col] = true;

        // Add smaller blob nearby
        for (var row = 1; row <= 2; row++)
        for (var col = 2; col <= 3; col++)
            _dataGrid[row, col] = true;
    }

    private void RecomputeVisualBitmasks()
    {
        if (_autoTileDef == null) return;

        var format = GetEffectiveFormat();

        if (format == "blob47")
        {
            // Blob47: Single-grid - compute 8-neighbor bitmasks
            _visualBitmasks = new int[DataGridRows, DataGridCols];
            for (var row = 0; row < DataGridRows; row++)
            for (var col = 0; col < DataGridCols; col++)
            {
                if (!_dataGrid[row, col])
                {
                    _visualBitmasks[row, col] = -1;
                    continue;
                }

                var position = new Vector2I(col, row);
                _visualBitmasks[row, col] = NeighborBitmask8.Compute(position, neighborPos =>
                {
                    if (neighborPos.X < 0 || neighborPos.X >= DataGridCols ||
                        neighborPos.Y < 0 || neighborPos.Y >= DataGridRows)
                        return false;
                    return _dataGrid[neighborPos.Y, neighborPos.X];
                });
            }
        }
        else
        {
            // Corner16/Edge16: Dual-grid
            _visualBitmasks = DualGridAutoTile.ComputeAllBitmasks(_dataGrid);
        }
    }

    private void EmitAutoTileInfo()
    {
        if (_autoTileDef == null) return;

        var filledCount = 0;
        for (var r = 0; r < DataGridRows; r++)
        for (var c = 0; c < DataGridCols; c++)
            if (_dataGrid[r, c])
                filledCount++;

        var format = GetEffectiveFormat();
        var isOverridden = !string.IsNullOrEmpty(_formatOverride);
        var formatText = isOverridden ? $"{format} (overridden)" : format;
        var info = $"Auto-tile: {_autoTileDef.Name} ({formatText}). " +
                   $"Data grid: {filledCount}/{DataGridRows * DataGridCols} cells. Click to toggle.";
        EmitSignal(SignalName.InfoChanged, info);
    }

    private void UpdateSize()
    {
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;

        if (_isAutoTileMode && _autoTileDef != null)
        {
            // Auto-tile mode: size includes visual grid (data grid + 1)
            CustomMinimumSize = new Vector2(
                (DataGridCols + 1) * scaledTileSize.X,
                (DataGridRows + 1) * scaledTileSize.Y
            );
            return;
        }

        if (_mapData == null)
        {
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var bounds = _mapData.GetBounds();
        var width = bounds.Max.X - bounds.Min.X + 1;
        var height = bounds.Max.Y - bounds.Min.Y + 1;

        CustomMinimumSize = new Vector2(
            width * scaledTileSize.X,
            height * scaledTileSize.Y
        );
    }

    private void EmitInfo()
    {
        if (_mapData == null)
        {
            EmitSignal(SignalName.InfoChanged, "No TMX file loaded.");
            return;
        }

        var bounds = _mapData.GetBounds();
        var tileCount = 0;
        foreach (var _ in _mapData.GetAllTiles())
            tileCount++;

        var info = $"TMX loaded: {_mapData.Tilesets.Count} tileset(s), {tileCount} tiles. " +
                   $"Bounds: ({bounds.Min.X},{bounds.Min.Y}) to ({bounds.Max.X},{bounds.Max.Y}). " +
                   "Hover for tile info.";
        EmitSignal(SignalName.InfoChanged, info);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_isAutoTileMode && _autoTileDef != null)
        {
            HandleAutoTileModeInput(@event);
            return;
        }

        if (_mapData == null) return;

        if (@event is InputEventMouseMotion mouseMotion)
        {
            UpdateTooltip(mouseMotion.Position);
        }
    }

    private void HandleAutoTileModeInput(InputEvent @event)
    {
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var halfTile = scaledTileSize / 2;

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            // Data grid is offset by half a tile
            var adjustedPos = mouseButton.Position - halfTile;
            var col = (int)(adjustedPos.X / scaledTileSize.X);
            var row = (int)(adjustedPos.Y / scaledTileSize.Y);

            if (row >= 0 && row < DataGridRows && col >= 0 && col < DataGridCols)
            {
                _dataGrid[row, col] = !_dataGrid[row, col];
                RecomputeVisualBitmasks();
                QueueRedraw();
                EmitAutoTileInfo();
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            UpdateAutoTileTooltip(mouseMotion.Position);
        }
    }

    private void UpdateAutoTileTooltip(Vector2 localPos)
    {
        if (_autoTileDef == null)
        {
            TooltipText = "";
            return;
        }

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var halfTile = scaledTileSize / 2;
        var format = GetEffectiveFormat();

        int bitmask = -1;

        if (format == "blob47")
        {
            // Blob format: data grid cells
            var dataCol = (int)((localPos.X - halfTile.X) / scaledTileSize.X);
            var dataRow = (int)((localPos.Y - halfTile.Y) / scaledTileSize.Y);

            if (dataRow >= 0 && dataRow < DataGridRows && dataCol >= 0 && dataCol < DataGridCols)
            {
                bitmask = _visualBitmasks[dataRow, dataCol];
            }
        }
        else
        {
            // Dual-grid formats: visual grid cells
            var vx = (int)(localPos.X / scaledTileSize.X);
            var vy = (int)(localPos.Y / scaledTileSize.Y);

            if (vy >= 0 && vy < VisualGridRows && vx >= 0 && vx < VisualGridCols)
            {
                bitmask = _visualBitmasks[vy, vx];
            }
        }

        if (bitmask >= 0)
        {
            var atlasCoords = _autoTileDef.GetAutoTileCoords(bitmask);
            var variantDef = _autoTileDef.GetVariantDefinition(bitmask);
            var isValid = HasVariantForBitmask(bitmask);

            var tooltip = $"Bitmask: {bitmask}\n";
            tooltip += $"Atlas: ({atlasCoords.X}, {atlasCoords.Y})\n";

            if (variantDef.HasValue)
            {
                var vd = variantDef.Value;
                if (vd.Size.X > 1 || vd.Size.Y > 1)
                    tooltip += $"Size: {vd.Size.X}x{vd.Size.Y}\n";
                if (vd.Offset.X != 0 || vd.Offset.Y != 0)
                    tooltip += $"Offset: ({vd.Offset.X}, {vd.Offset.Y})\n";
            }

            tooltip += $"Format: {format}\n";
            tooltip += isValid ? "Status: Valid" : "Status: Using default";
            TooltipText = tooltip;
        }
        else
        {
            TooltipText = format == "blob47" ? "Empty cell" : "";
        }
    }

    private bool HasVariantForBitmask(int bitmask)
    {
        if (_autoTileDef?.AutoTileVariants == null) return false;

        var format = GetEffectiveFormat();
        int variantIndex;

        if (format == "blob47")
        {
            variantIndex = NeighborBitmask8.GetBlobIndex(bitmask);
            if (variantIndex < 0) return false;
        }
        else
        {
            variantIndex = bitmask;
        }

        if (variantIndex < 0 || variantIndex >= _autoTileDef.AutoTileVariants.Length)
            return false;

        return _autoTileDef.AutoTileVariants[variantIndex].HasValue;
    }

    private void UpdateTooltip(Vector2 localPos)
    {
        if (_mapData == null)
        {
            TooltipText = "";
            return;
        }

        var bounds = _mapData.GetBounds();
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;

        // Convert screen position to tile coordinates
        var tileX = bounds.Min.X + (int)(localPos.X / scaledTileSize.X);
        var tileY = bounds.Min.Y + (int)(localPos.Y / scaledTileSize.Y);

        var resolution = _mapData.GetTileAt(tileX, tileY);
        if (resolution != null)
        {
            var tooltip = $"Position: ({tileX}, {tileY})\n";
            tooltip += $"Global ID: {resolution.GlobalTileId}\n";
            tooltip += $"Local ID: {resolution.LocalTileId}\n";
            tooltip += $"Atlas: ({resolution.AtlasCoords.X}, {resolution.AtlasCoords.Y})\n";
            tooltip += $"Tileset: {Path.GetFileName(resolution.Tileset.TsxPath)}";

            if (resolution.TileDefinition != null)
            {
                var def = resolution.TileDefinition;
                tooltip += $"\n\nTile: {def.Id}";
                if (!string.IsNullOrEmpty(def.AutoTileFormatName))
                    tooltip += $"\nFormat: {def.AutoTileFormatName}";
            }

            TooltipText = tooltip;
        }
        else
        {
            TooltipText = $"Position: ({tileX}, {tileY})\nEmpty";
        }
    }

    public override void _Draw()
    {
        if (_service == null) return;

        if (_isAutoTileMode && _autoTileDef != null)
        {
            DrawAutoTileMode();
            return;
        }

        if (_mapData == null) return;
        DrawTmxMapMode();
    }

    private void DrawAutoTileMode()
    {
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var halfTile = scaledTileSize / 2;
        var totalSize = CustomMinimumSize;

        // Draw background
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(0.15f, 0.15f, 0.15f, 1f));

        // Get texture for auto-tile
        var texture = GetAutoTileTexture();

        // Draw base tiles if available (from TSX)
        if (_baseTileDef != null && _baseTilesetRef != null)
        {
            var baseTexture = GetTextureForTileset(_baseTilesetRef);
            if (baseTexture != null)
            {
                var baseSrcRect = new Rect2(
                    _baseTileDef.AtlasCoords.X * _tileSize.X,
                    _baseTileDef.AtlasCoords.Y * _tileSize.Y,
                    _tileSize.X,
                    _tileSize.Y
                );

                for (var row = 0; row < DataGridRows + 1; row++)
                for (var col = 0; col < DataGridCols + 1; col++)
                {
                    var destRect = new Rect2(col * scaledTileSize.X, row * scaledTileSize.Y,
                        scaledTileSize.X, scaledTileSize.Y);
                    DrawTextureRectRegion(baseTexture, destRect, baseSrcRect);
                }
            }
        }

        // Draw auto-tile variants
        if (texture != null && _autoTileDef != null)
        {
            var format = GetEffectiveFormat();

            if (format == "blob47")
            {
                // Blob47: Single-grid - tiles aligned with data grid cells
                for (var row = 0; row < DataGridRows; row++)
                for (var col = 0; col < DataGridCols; col++)
                {
                    var bitmask = _visualBitmasks[row, col];
                    if (bitmask < 0) continue;

                    var atlasCoords = _autoTileDef.GetAutoTileCoords(bitmask);
                    var srcRect = new Rect2(
                        atlasCoords.X * _tileSize.X,
                        atlasCoords.Y * _tileSize.Y,
                        _tileSize.X,
                        _tileSize.Y
                    );

                    var destRect = new Rect2(
                        col * scaledTileSize.X + halfTile.X,
                        row * scaledTileSize.Y + halfTile.Y,
                        scaledTileSize.X,
                        scaledTileSize.Y
                    );

                    DrawTextureRectRegion(texture, destRect, srcRect);
                }
            }
            else
            {
                // Corner16/Edge16: Dual-grid - visual tiles at half-tile offset
                for (var vy = 0; vy < VisualGridRows; vy++)
                for (var vx = 0; vx < VisualGridCols; vx++)
                {
                    var bitmask = _visualBitmasks[vy, vx];
                    if (bitmask == 0) continue;

                    var atlasCoords = _autoTileDef.GetAutoTileCoords(bitmask);
                    var srcRect = new Rect2(
                        atlasCoords.X * _tileSize.X,
                        atlasCoords.Y * _tileSize.Y,
                        _tileSize.X,
                        _tileSize.Y
                    );

                    var destRect = new Rect2(
                        vx * scaledTileSize.X,
                        vy * scaledTileSize.Y,
                        scaledTileSize.X,
                        scaledTileSize.Y
                    );

                    DrawTextureRectRegion(texture, destRect, srcRect);
                }
            }
        }

        // Draw data grid overlay
        if (_showDataGrid)
        {
            for (var row = 0; row < DataGridRows; row++)
            for (var col = 0; col < DataGridCols; col++)
            {
                var destRect = new Rect2(
                    col * scaledTileSize.X + halfTile.X,
                    row * scaledTileSize.Y + halfTile.Y,
                    scaledTileSize.X,
                    scaledTileSize.Y
                );

                var isFilled = _dataGrid[row, col];
                var color = isFilled
                    ? new Color(0.2f, 0.8f, 0.2f, 0.25f)
                    : new Color(0.8f, 0.2f, 0.2f, 0.1f);

                DrawRect(destRect, color);
                DrawRect(destRect, new Color(0.5f, 0.5f, 0.5f, 0.3f), false, 1.0f);
            }
        }

        // Draw border
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(0.7f, 0.7f, 0.7f, 0.5f), false, 2.0f);
    }

    private Texture2D? GetAutoTileTexture()
    {
        if (_autoTileDef == null || _currentTilesetRef == null) return null;
        return GetTextureForTileset(_currentTilesetRef);
    }

    /// <summary>
    /// Load and cache the texture for a tileset from its TilesetPath (TSX image source).
    /// TilesetPath is already resolved to an absolute path by TiledTilesetLoader.LoadFromTsx.
    /// </summary>
    private Texture2D? GetTextureForTileset(TiledTilesetLoader.TmxTilesetReference tilesetRef)
    {
        var absoluteTexturePath = tilesetRef.TilesetData.TilesetPath;
        GD.Print($"[TmxPreviewControl] GetTextureForTileset: TilesetPath='{absoluteTexturePath}'");
        if (string.IsNullOrEmpty(absoluteTexturePath))
        {
            GD.PrintErr("[TmxPreviewControl] TilesetPath is empty!");
            return null;
        }

        // Check cache first
        if (_textureCache.TryGetValue(absoluteTexturePath, out var cached))
            return cached;

        // Load texture from file
        Texture2D? texture = null;
        try
        {
            GD.Print($"[TmxPreviewControl] Checking if file exists: {absoluteTexturePath}");
            if (File.Exists(absoluteTexturePath))
            {
                GD.Print($"[TmxPreviewControl] File exists, loading image...");
                var image = Image.LoadFromFile(absoluteTexturePath);
                if (image != null)
                {
                    texture = ImageTexture.CreateFromImage(image);
                    GD.Print($"[TmxPreviewControl] Texture loaded successfully: {texture.GetWidth()}x{texture.GetHeight()}");
                }
                else
                {
                    GD.PrintErr("[TmxPreviewControl] Image.LoadFromFile returned null");
                }
            }
            else
            {
                GD.PrintErr($"[TmxPreviewControl] Texture file not found: {absoluteTexturePath}");
            }
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[TmxPreviewControl] Failed to load texture: {ex.Message}\n{ex.StackTrace}");
        }

        _textureCache[absoluteTexturePath] = texture;
        return texture;
    }

    private void DrawTmxMapMode()
    {
        if (_mapData == null) return;

        var bounds = _mapData.GetBounds();
        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;

        // Draw background
        var totalSize = CustomMinimumSize;
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(0.15f, 0.15f, 0.15f, 1f));

        // Get base tile texture if available (from TSX)
        Texture2D? baseTexture = null;
        Rect2? baseSrcRect = null;
        if (_baseTileDef != null && _baseTilesetRef != null)
        {
            baseTexture = GetTextureForTileset(_baseTilesetRef);
            if (baseTexture != null)
            {
                baseSrcRect = new Rect2(
                    _baseTileDef.AtlasCoords.X * _tileSize.X,
                    _baseTileDef.AtlasCoords.Y * _tileSize.Y,
                    _tileSize.X,
                    _tileSize.Y
                );
            }
        }

        // Draw all tiles from the TMX
        foreach (var (coord, resolution) in _mapData.GetAllTiles())
        {
            // Get the tileset's texture from its TSX-referenced image path
            var tileset = resolution.Tileset;
            var texture = GetTextureForTileset(tileset);
            if (texture == null) continue;

            // Calculate positions
            var screenX = (coord.X - bounds.Min.X) * scaledTileSize.X;
            var screenY = (coord.Y - bounds.Min.Y) * scaledTileSize.Y;
            var destRect = new Rect2(screenX, screenY, scaledTileSize.X, scaledTileSize.Y);

            // Check if this tile is a transparent/compositable tile that needs a base underneath
            var tileDef = resolution.TileDefinition;
            var isTransparentTile = tileDef?.IsTransparent == true ||
                                    tileDef?.IsCompositable == true ||
                                    tileDef?.HasAutoTileVariants == true;

            // Draw base tile first for transparent tiles
            if (isTransparentTile)
            {
                if (baseTexture != null && baseSrcRect.HasValue)
                {
                    // Draw base terrain
                    DrawTextureRectRegion(baseTexture, destRect, baseSrcRect.Value);
                }
                else
                {
                    // Draw checkerboard pattern for transparent tiles without base
                    DrawCheckerboard(destRect, coord);
                }
            }

            // Calculate source rect from atlas coords
            var atlasCoords = resolution.AtlasCoords;
            var actualTileSize = _tileSize;

            var srcRect = new Rect2(
                atlasCoords.X * actualTileSize.X,
                atlasCoords.Y * actualTileSize.Y,
                actualTileSize.X,
                actualTileSize.Y
            );

            // Draw the tile (on top of base if transparent)
            DrawTextureRectRegion(texture, destRect, srcRect);
        }

        // Draw grid overlay
        var gridColor = new Color(0.5f, 0.5f, 0.5f, 0.2f);
        var width = bounds.Max.X - bounds.Min.X + 1;
        var height = bounds.Max.Y - bounds.Min.Y + 1;

        for (var x = 0; x <= width; x++)
        {
            DrawLine(
                new Vector2(x * scaledTileSize.X, 0),
                new Vector2(x * scaledTileSize.X, height * scaledTileSize.Y),
                gridColor
            );
        }

        for (var y = 0; y <= height; y++)
        {
            DrawLine(
                new Vector2(0, y * scaledTileSize.Y),
                new Vector2(width * scaledTileSize.X, y * scaledTileSize.Y),
                gridColor
            );
        }

        // Draw border
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(0.7f, 0.7f, 0.7f, 0.5f), false, 2.0f);
    }

    private void DrawCheckerboard(Rect2 destRect, Vector2I coord)
    {
        // Draw a checkerboard pattern to show transparency
        var lightColor = new Color(0.4f, 0.4f, 0.4f, 1f);
        var darkColor = new Color(0.25f, 0.25f, 0.25f, 1f);

        var checkerSize = destRect.Size / 4; // 4x4 checker grid per tile
        for (var cy = 0; cy < 4; cy++)
        {
            for (var cx = 0; cx < 4; cx++)
            {
                var isLight = ((coord.X + coord.Y + cx + cy) % 2) == 0;
                var checkerRect = new Rect2(
                    destRect.Position.X + cx * checkerSize.X,
                    destRect.Position.Y + cy * checkerSize.Y,
                    checkerSize.X,
                    checkerSize.Y
                );
                DrawRect(checkerRect, isLight ? lightColor : darkColor);
            }
        }
    }
}
#endif
