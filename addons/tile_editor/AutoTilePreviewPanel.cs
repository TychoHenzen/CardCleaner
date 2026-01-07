#if TOOLS
using System;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
///     Panel showing auto-tile variants in an interactive dual-grid preview.
///     Uses the dual-tilemap technique where the visual grid is offset by half a tile
///     from the data grid, ensuring valid auto-tile states at all times.
/// </summary>
[Tool]
public partial class AutoTilePreviewPanel : ScrollContainer
{
    private readonly TileEditorService? _service;
    private OptionButton? _tileSelector;
    private OptionButton? _baseTileSelector;
    private OptionButton? _formatOverrideDropdown;
    private HSlider? _scaleSlider;
    private Label? _scaleLabel;
    private Label? _infoLabel;
    private CheckBox? _showDataGridCheckbox;
    private DualGridAutoTilePreview? _mapPreview;
    private string? _selectedTileId;
    private string? _selectedBaseTileId;
    private string? _formatOverride; // null = use tile's format

    // Required by Godot for [Tool] classes
    public AutoTilePreviewPanel() { }

    public AutoTilePreviewPanel(TileEditorService service)
    {
        _service = service;
    }

    public override void _Ready()
    {
        // Guard for Godot's parameterless constructor case
        if (_service == null) return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;

        var mainVBox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(mainVBox);

        // Header
        var header = new Label
        {
            Text = "Auto-Tile Preview (Dual Grid)",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 16);
        mainVBox.AddChild(header);

        mainVBox.AddChild(new HSeparator());

        // Tile selector row
        var tileRow = new HBoxContainer();
        tileRow.AddChild(new Label { Text = "Overlay tile:", CustomMinimumSize = new Vector2(100, 0) });
        _tileSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tileSelector.ItemSelected += OnTileSelected;
        tileRow.AddChild(_tileSelector);
        mainVBox.AddChild(tileRow);

        // Base tile selector row
        var baseRow = new HBoxContainer();
        baseRow.AddChild(new Label { Text = "Base tile:", CustomMinimumSize = new Vector2(100, 0) });
        _baseTileSelector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _baseTileSelector.ItemSelected += OnBaseTileSelected;
        baseRow.AddChild(_baseTileSelector);
        mainVBox.AddChild(baseRow);

        // Format override selector row
        var formatRow = new HBoxContainer();
        formatRow.AddChild(new Label { Text = "Format:", CustomMinimumSize = new Vector2(100, 0) });
        _formatOverrideDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Override format for preview (uses tile's format if not set)"
        };
        PopulateFormatDropdown();
        _formatOverrideDropdown.ItemSelected += OnFormatOverrideSelected;
        formatRow.AddChild(_formatOverrideDropdown);
        mainVBox.AddChild(formatRow);

        // Scale slider row
        var scaleRow = new HBoxContainer();
        scaleRow.AddChild(new Label { Text = "Scale:", CustomMinimumSize = new Vector2(100, 0) });
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
        mainVBox.AddChild(scaleRow);

        // Show data grid checkbox
        var optionsRow = new HBoxContainer();
        _showDataGridCheckbox = new CheckBox { Text = "Show data grid overlay", ButtonPressed = true };
        _showDataGridCheckbox.Toggled += OnShowDataGridToggled;
        optionsRow.AddChild(_showDataGridCheckbox);
        mainVBox.AddChild(optionsRow);

        mainVBox.AddChild(new HSeparator());

        // Info label
        _infoLabel = new Label
        {
            Text = "Click cells to toggle. Visual tiles update automatically.",
            Modulate = new Color(0.8f, 0.8f, 0.8f),
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        mainVBox.AddChild(_infoLabel);

        // Map preview in scroll container
        var previewScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto
        };

        _mapPreview = new DualGridAutoTilePreview(_service);
        _mapPreview.InfoChanged += OnInfoChanged;
        previewScroll.AddChild(_mapPreview);
        mainVBox.AddChild(previewScroll);

        // Populate dropdowns after service loads
        CallDeferred(MethodName.PopulateDropdowns);
    }

    private void OnInfoChanged(string info)
    {
        if (_infoLabel != null)
            _infoLabel.Text = info;
    }

    private void OnShowDataGridToggled(bool pressed)
    {
        _mapPreview?.SetShowDataGrid(pressed);
    }

    private void PopulateDropdowns()
    {
        PopulateTileSelector();
        PopulateBaseTileSelector();
        PopulateFormatDropdown();
    }

    private void PopulateFormatDropdown()
    {
        if (_formatOverrideDropdown == null || _service == null) return;

        _formatOverrideDropdown.Clear();

        // First option: use tile's configured format
        _formatOverrideDropdown.AddItem("(Use tile's format)", 0);
        _formatOverrideDropdown.SetItemMetadata(0, "");

        // Add all available formats from registry
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
        RefreshPreview();
    }

    private void PopulateTileSelector()
    {
        _tileSelector!.Clear();
        _tileSelector.AddItem("-- Select tile --", 0);

        var index = 1;
        foreach (var tile in _service.AllTiles)
        {
            if (tile.HasAutoTileVariants)
            {
                _tileSelector.AddItem($"{tile.Name} ({tile.Id})", index);
                _tileSelector.SetItemMetadata(index, tile.Id);
                index++;
            }
        }
    }

    private void PopulateBaseTileSelector()
    {
        _baseTileSelector!.Clear();
        _baseTileSelector.AddItem("-- Select base --", 0);

        var index = 1;
        foreach (var tile in _service.AllTiles)
        {
            if (tile.Layer.Equals("terrain", StringComparison.OrdinalIgnoreCase))
            {
                _baseTileSelector.AddItem($"{tile.Name} ({tile.Id})", index);
                _baseTileSelector.SetItemMetadata(index, tile.Id);
                index++;
            }
        }
    }

    private void OnTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedTileId = null;
            ClearPreview();
            return;
        }

        _selectedTileId = _tileSelector!.GetItemMetadata((int)index).AsString();
        RefreshPreview();
    }

    private void OnBaseTileSelected(long index)
    {
        if (index == 0)
        {
            _selectedBaseTileId = null;
        }
        else
        {
            _selectedBaseTileId = _baseTileSelector!.GetItemMetadata((int)index).AsString();
        }
        RefreshPreview();
    }

    private void OnScaleChanged(double value)
    {
        _scaleLabel!.Text = $"{value:F1}x";
        _mapPreview?.SetScale((float)value);
    }

    private void ClearPreview()
    {
        _infoLabel!.Text = "Select a tile with auto-tile variants to preview.";
        _mapPreview?.ClearPreview();
    }

    private void RefreshPreview()
    {
        if (string.IsNullOrEmpty(_selectedTileId))
        {
            ClearPreview();
            return;
        }

        var tile = _service.GetTile(_selectedTileId);
        if (tile == null || !tile.HasAutoTileVariants)
        {
            _infoLabel!.Text = "Selected tile has no auto-tile variants.";
            _mapPreview?.ClearPreview();
            return;
        }

        // Use format override if set, otherwise use tile's configured format
        var formatName = _formatOverride ?? tile.AutoTileFormat?.ToLowerInvariant() ?? "corner16";
        var format = _service.GetFormatDefinition(formatName);
        var formatDisplayName = format != null ? _service.GetFormatDisplayName(formatName) : formatName;

        var isOverridden = _formatOverride != null;
        _infoLabel!.Text = isOverridden
            ? $"Click cells to toggle ({formatDisplayName} - overridden). Visual grid shows correct transitions."
            : $"Click cells to toggle ({formatDisplayName}). Visual grid shows correct transitions.";

        EditableTile? baseTile = null;
        if (!string.IsNullOrEmpty(_selectedBaseTileId))
        {
            baseTile = _service.GetTile(_selectedBaseTileId);
        }

        _mapPreview?.SetTiles(tile, baseTile, (float)_scaleSlider!.Value, _formatOverride);
    }

    public void Refresh()
    {
        PopulateDropdowns();
        if (!string.IsNullOrEmpty(_selectedTileId))
        {
            RefreshPreview();
        }
    }
}

/// <summary>
///     Interactive dual-grid preview for exploring auto-tile configurations.
///     Data grid (15x10): What the user toggles - is this cell "filled"?
///     Visual grid (16x11): Rendered at half-tile offset, each tile samples 4 corner data cells.
///     This approach prevents invalid auto-tile states because visual tiles always
///     compute bitmasks from actual data cell states.
/// </summary>
[Tool]
public partial class DualGridAutoTilePreview : Control
{
    private const int DataGridCols = 15;
    private const int DataGridRows = 10;
    private const int VisualGridCols = DataGridCols + 1; // 16
    private const int VisualGridRows = DataGridRows + 1; // 11

    [Signal]
    public delegate void InfoChangedEventHandler(string info);

    private readonly TileEditorService? _service;
    private EditableTile? _overlayTile;
    private EditableTile? _baseTile;
    private float _scale = 2f;
    private Vector2I _tileSize = new(16, 16);
    private bool _showDataGrid = true;
    private string? _formatOverride; // null = use tile's format

    // Data grid: what the user toggles (true = filled with overlay tile)
    private readonly bool[,] _dataGrid = new bool[DataGridRows, DataGridCols];

    // Cached bitmasks for visual grid (recomputed when data grid changes)
    private int[,] _visualBitmasks = new int[VisualGridRows, VisualGridCols];

    // Required by Godot for [Tool] classes
    public DualGridAutoTilePreview() { }

    public DualGridAutoTilePreview(TileEditorService service)
    {
        _service = service;
        TextureFilter = TextureFilterEnum.Nearest;
        MouseFilter = MouseFilterEnum.Stop;

        // Initialize with some sample data to show the dual-grid effect
        InitializeSamplePattern();
    }

    private void InitializeSamplePattern()
    {
        // Create an interesting initial pattern
        // A rectangular blob in the center
        for (var row = 3; row <= 6; row++)
        for (var col = 5; col <= 9; col++)
            _dataGrid[row, col] = true;

        // Add a smaller blob nearby
        for (var row = 1; row <= 2; row++)
        for (var col = 2; col <= 3; col++)
            _dataGrid[row, col] = true;

        RecomputeVisualBitmasks();
    }

    public void SetScale(float scale)
    {
        _scale = scale;
        UpdateSize();
        QueueRedraw();
    }

    public void SetShowDataGrid(bool show)
    {
        _showDataGrid = show;
        QueueRedraw();
    }

    public void SetTiles(EditableTile? overlayTile, EditableTile? baseTile, float scale, string? formatOverride = null)
    {
        _overlayTile = overlayTile;
        _baseTile = baseTile;
        _scale = scale;
        _formatOverride = formatOverride;
        _tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        UpdateSize();
        RecomputeVisualBitmasks();
        QueueRedraw();
        EmitInfo();
    }

    /// <summary>
    /// Gets the effective format name being used (override or tile's format).
    /// </summary>
    private string GetEffectiveFormat()
    {
        if (!string.IsNullOrEmpty(_formatOverride))
            return _formatOverride;
        return _overlayTile?.AutoTileFormat?.ToLowerInvariant() ?? "corner16";
    }

    public void ClearPreview()
    {
        _overlayTile = null;
        _baseTile = null;
        UpdateSize();
        QueueRedraw();
    }

    private void UpdateSize()
    {
        if (_overlayTile == null)
        {
            CustomMinimumSize = Vector2.Zero;
            return;
        }

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        // Size includes visual grid which extends half a tile beyond data grid on all sides
        CustomMinimumSize = new Vector2(
            (DataGridCols + 1) * scaledTileSize.X,
            (DataGridRows + 1) * scaledTileSize.Y
        );
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (_overlayTile == null) return;

        if (@event is InputEventMouseButton mouseButton &&
            mouseButton.Pressed &&
            mouseButton.ButtonIndex == MouseButton.Left)
        {
            var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;

            // Data grid is offset by half a tile (visual grid starts at 0,0)
            // So data cell (0,0) is at pixel position (halfTile, halfTile)
            var halfTile = scaledTileSize / 2;
            var adjustedPos = mouseButton.Position - halfTile;

            var col = (int)(adjustedPos.X / scaledTileSize.X);
            var row = (int)(adjustedPos.Y / scaledTileSize.Y);

            // Check if within data grid bounds
            if (row >= 0 && row < DataGridRows && col >= 0 && col < DataGridCols)
            {
                _dataGrid[row, col] = !_dataGrid[row, col];
                RecomputeVisualBitmasks();
                QueueRedraw();
                EmitInfo();
            }
        }
        // Show tooltip on hover with variant info
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            UpdateTooltip(mouseMotion.Position);
        }
    }

    private void RecomputeVisualBitmasks()
    {
        var format = GetEffectiveFormat();

        if (format == "blob47")
        {
            // Blob47: Single-grid technique - compute 8-neighbor bitmasks for each data cell
            // The bitmask array matches the data grid dimensions
            _visualBitmasks = new int[DataGridRows, DataGridCols];

            for (var row = 0; row < DataGridRows; row++)
            for (var col = 0; col < DataGridCols; col++)
            {
                if (!_dataGrid[row, col])
                {
                    _visualBitmasks[row, col] = -1; // Mark as empty (no overlay)
                    continue;
                }

                // Compute 8-neighbor blob mask using NeighborBitmask8
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
            // Corner16/Edge16: Dual-grid technique - visual grid offset by half tile
            _visualBitmasks = DualGridAutoTile.ComputeAllBitmasks(_dataGrid);
        }
    }

    private void EmitInfo()
    {
        if (_overlayTile == null) return;

        var filledCount = 0;
        for (var r = 0; r < DataGridRows; r++)
        for (var c = 0; c < DataGridCols; c++)
            if (_dataGrid[r, c])
                filledCount++;

        var format = GetEffectiveFormat();
        var isOverridden = !string.IsNullOrEmpty(_formatOverride);
        var formatText = isOverridden ? $"{format} (overridden)" : format;
        var info = $"Data grid: {filledCount}/{DataGridRows * DataGridCols} cells filled. " +
                   $"Format: {formatText}. Click to toggle cells.";
        EmitSignal(SignalName.InfoChanged, info);
    }

    public override void _Draw()
    {
        // Guard for Godot's parameterless constructor case
        if (_service == null || _overlayTile == null)
            return;

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var halfTile = scaledTileSize / 2;

        var overlayTexture = _service.GetTileTexture(_overlayTile);
        Texture2D? baseTexture = null;
        if (_baseTile != null)
        {
            baseTexture = _service.GetTileTexture(_baseTile);
        }

        // Draw base tiles for entire area
        if (baseTexture != null && _baseTile != null)
        {
            // Calculate actual source tile size accounting for base tile's SourceScale
            var baseActualTileSize = new Vector2I(
                (int)(_tileSize.X / _baseTile.SourceScale),
                (int)(_tileSize.Y / _baseTile.SourceScale)
            );
            var baseCoords = new Vector2I(_baseTile.AtlasX, _baseTile.AtlasY);
            var baseSrcRect = new Rect2(
                baseCoords.X * baseActualTileSize.X,
                baseCoords.Y * baseActualTileSize.Y,
                baseActualTileSize.X,
                baseActualTileSize.Y);

            for (var row = 0; row < DataGridRows + 1; row++)
            for (var col = 0; col < DataGridCols + 1; col++)
            {
                var destRect = new Rect2(col * scaledTileSize.X, row * scaledTileSize.Y,
                    scaledTileSize.X, scaledTileSize.Y);
                DrawTextureRectRegion(baseTexture, destRect, baseSrcRect);
            }
        }

        // Draw overlay tiles - different grid alignment based on format
        if (overlayTexture != null)
        {
            // Calculate actual source tile size accounting for SourceScale
            // For scale 2.0 (8px sources): 16/2.0 = 8px tiles
            // For scale 0.5 (32px sources): 16/0.5 = 32px tiles
            var overlayActualTileSize = new Vector2I(
                (int)(_tileSize.X / _overlayTile.SourceScale),
                (int)(_tileSize.Y / _overlayTile.SourceScale)
            );

            var format = GetEffectiveFormat();

            if (format == "blob47")
            {
                // Blob47: Single-grid - tiles aligned with data grid cells
                // Data grid is offset by half tile from visual origin
                for (var row = 0; row < DataGridRows; row++)
                for (var col = 0; col < DataGridCols; col++)
                {
                    var bitmask = _visualBitmasks[row, col];
                    if (bitmask < 0) continue; // Empty cell (not filled)

                    var variantInfo = GetVariantInfo(bitmask);
                    var srcRect = new Rect2(
                        variantInfo.AtlasCoords.X * overlayActualTileSize.X,
                        variantInfo.AtlasCoords.Y * overlayActualTileSize.Y,
                        overlayActualTileSize.X * variantInfo.Size.X,
                        overlayActualTileSize.Y * variantInfo.Size.Y
                    );

                    // Data cell position: offset by half tile from origin
                    // Scale destination by variant size from format
                    var destRect = new Rect2(
                        col * scaledTileSize.X + halfTile.X,
                        row * scaledTileSize.Y + halfTile.Y,
                        scaledTileSize.X * variantInfo.Size.X,
                        scaledTileSize.Y * variantInfo.Size.Y
                    );

                    DrawTextureRectRegion(overlayTexture, destRect, srcRect);
                }
            }
            else
            {
                // Corner16/Edge16: Dual-grid - visual tiles at half-tile offset from data
                for (var vy = 0; vy < VisualGridRows; vy++)
                for (var vx = 0; vx < VisualGridCols; vx++)
                {
                    var bitmask = _visualBitmasks[vy, vx];
                    if (bitmask == 0) continue; // No corners filled, skip

                    var variantInfo = GetVariantInfo(bitmask);
                    var srcRect = new Rect2(
                        variantInfo.AtlasCoords.X * overlayActualTileSize.X,
                        variantInfo.AtlasCoords.Y * overlayActualTileSize.Y,
                        overlayActualTileSize.X * variantInfo.Size.X,
                        overlayActualTileSize.Y * variantInfo.Size.Y
                    );

                    // Visual tile position: offset by -half tile from data grid
                    // Visual (0,0) is at pixel (-halfTile, -halfTile)
                    // We render at (vx * tileSize - halfTile, vy * tileSize - halfTile)
                    // But since our control starts at 0,0, we shift everything by +halfTile
                    // So visual (0,0) renders at (0,0) and data grid renders at (halfTile, halfTile)
                    // Scale destination by variant size from format
                    var destRect = new Rect2(
                        vx * scaledTileSize.X,
                        vy * scaledTileSize.Y,
                        scaledTileSize.X * variantInfo.Size.X,
                        scaledTileSize.Y * variantInfo.Size.Y
                    );

                    DrawTextureRectRegion(overlayTexture, destRect, srcRect);
                }
            }
        }

        // Draw data grid overlay (shows which cells are "filled")
        if (_showDataGrid)
        {
            for (var row = 0; row < DataGridRows; row++)
            for (var col = 0; col < DataGridCols; col++)
            {
                // Data grid is offset by half a tile
                var destRect = new Rect2(
                    col * scaledTileSize.X + halfTile.X,
                    row * scaledTileSize.Y + halfTile.Y,
                    scaledTileSize.X,
                    scaledTileSize.Y
                );

                var isFilled = _dataGrid[row, col];
                var color = isFilled
                    ? new Color(0.2f, 0.8f, 0.2f, 0.25f) // Green for filled
                    : new Color(0.8f, 0.2f, 0.2f, 0.1f); // Faint red for empty

                DrawRect(destRect, color);

                // Draw border for data cells
                DrawRect(destRect, new Color(0.5f, 0.5f, 0.5f, 0.3f), false, 1.0f);
            }
        }

        // Draw outer boundary to show visual grid extent
        var totalSize = new Vector2(
            (DataGridCols + 1) * scaledTileSize.X,
            (DataGridRows + 1) * scaledTileSize.Y
        );
        DrawRect(new Rect2(Vector2.Zero, totalSize), new Color(0.7f, 0.7f, 0.7f, 0.5f), false, 2.0f);
    }

    /// <summary>
    /// Gets complete variant information including size and offset for a bitmask.
    /// Atlas coordinates come from the tile's AutoTileVariants array.
    /// Size comes from the format's VariantMappings (defines variant dimensions like 1x3 for tall platforms).
    /// Offset is per-tile only (from CustomVariantDefinitions), defaults to 0,0.
    /// </summary>
    private (Vector2I AtlasCoords, Vector2I Size, Vector2I Offset, bool IsValid) GetVariantInfo(int bitmask)
    {
        var defaultCoords = new Vector2I(_overlayTile?.AtlasX ?? 0, _overlayTile?.AtlasY ?? 0);
        var defaultSize = new Vector2I(1, 1);
        var defaultOffset = Vector2I.Zero;

        if (_overlayTile == null)
        {
            return (defaultCoords, defaultSize, defaultOffset, false);
        }

        var format = GetEffectiveFormat();
        int variantIndex;

        if (format == "blob47")
        {
            variantIndex = NeighborBitmask8.GetBlobIndex(bitmask);
            if (variantIndex < 0) return (defaultCoords, defaultSize, defaultOffset, false);
        }
        else
        {
            variantIndex = bitmask;
        }

        // Check format registry to see if this bitmask is allowed and get format-level size
        var formatDef = _service.GetFormatDefinition(format);
        if (formatDef != null && !formatDef.AllowedBitmasks.Contains(variantIndex))
        {
            return (defaultCoords, defaultSize, defaultOffset, false);
        }

        // Get size from format's variant mappings (format defines variant dimensions)
        var variantSize = defaultSize;
        if (formatDef != null)
        {
            var formatVariant = formatDef.GetVariant(variantIndex);
            if (formatVariant.HasValue && (formatVariant.Value.Size.X > 0 || formatVariant.Value.Size.Y > 0))
            {
                variantSize = formatVariant.Value.Size;
            }
        }

        // Check for custom variant definition on the tile (can override atlas coords and offset)
        if (_overlayTile.CustomVariantDefinitions != null &&
            _overlayTile.CustomVariantDefinitions.TryGetValue(variantIndex, out var customDef))
        {
            // Custom definition provides atlas coords and offset; size comes from format
            return (customDef.AtlasCoords, variantSize, customDef.Offset, true);
        }

        // Get atlas coords from tile's AutoTileVariants array; size from format
        if (_overlayTile.AutoTileVariants != null &&
            variantIndex >= 0 && variantIndex < _overlayTile.AutoTileVariants.Length &&
            _overlayTile.AutoTileVariants[variantIndex].HasValue)
        {
            return (_overlayTile.AutoTileVariants[variantIndex]!.Value, variantSize, defaultOffset, true);
        }

        return (defaultCoords, defaultSize, defaultOffset, false);
    }

    /// <summary>
    /// Checks if a bitmask is valid/allowed for the current format.
    /// </summary>
    private bool IsBitmaskAllowed(int bitmask)
    {
        var format = GetEffectiveFormat();
        var formatDef = _service.GetFormatDefinition(format);

        if (formatDef == null)
        {
            // Default: allow all bitmasks 0-15 for corner/edge, or valid blob indices
            if (format == "blob47")
            {
                var index = NeighborBitmask8.GetBlobIndex(bitmask);
                return index >= 0 && index < 47;
            }
            return bitmask >= 0 && bitmask < 16;
        }

        // Check against format's allowed bitmasks
        int variantIndex = format == "blob47"
            ? NeighborBitmask8.GetBlobIndex(bitmask)
            : bitmask;

        return formatDef.AllowedBitmasks.Contains(variantIndex);
    }

    private void UpdateTooltip(Vector2 localPos)
    {
        if (_overlayTile == null)
        {
            TooltipText = "";
            return;
        }

        var scaledTileSize = new Vector2(_tileSize.X, _tileSize.Y) * _scale;
        var halfTile = scaledTileSize / 2;

        // Determine which visual cell we're hovering over
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
            var info = GetVariantInfo(bitmask);
            var tooltip = $"Bitmask: {bitmask}\n";
            tooltip += $"Atlas: ({info.AtlasCoords.X}, {info.AtlasCoords.Y})\n";
            if (info.Size.X > 1 || info.Size.Y > 1)
            {
                tooltip += $"Size: {info.Size.X}x{info.Size.Y}\n";
            }
            if (info.Offset.X != 0 || info.Offset.Y != 0)
            {
                tooltip += $"Offset: ({info.Offset.X}, {info.Offset.Y})\n";
            }
            tooltip += info.IsValid ? "Status: Valid" : "Status: Using default";
            TooltipText = tooltip;
        }
        else
        {
            TooltipText = "";
        }
    }
}
#endif
