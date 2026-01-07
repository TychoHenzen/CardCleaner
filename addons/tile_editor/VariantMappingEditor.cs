#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Editor component for configuring auto-tile variant mappings.
/// Displays a grid of variants with atlas coord pickers, size/offset inputs, and previews.
/// </summary>
[Tool]
public partial class VariantMappingEditor : VBoxContainer
{
    [Signal]
    public delegate void VariantChangedEventHandler(int bitmask);

    [Signal]
    public delegate void VariantsModifiedEventHandler();

    private readonly TileEditorService? _service;
    private ScrollContainer? _scrollContainer;
    private GridContainer? _variantGrid;
    private Label? _headerLabel;
    private Label? _infoLabel;

    // Current state
    private EditableTile? _currentTile;
    private AutoTileFormatDefinition? _currentFormat;
    private bool _isUpdating;
    private bool _isReadOnly;

    // Variant UI elements keyed by bitmask
    private readonly Dictionary<int, VariantCell> _variantCells = new();

    // Required by Godot for [Tool] classes
    public VariantMappingEditor() { }

    public VariantMappingEditor(TileEditorService service)
    {
        _service = service;
        InitializeUI();
    }

    public override void _Ready()
    {
        // UI is already initialized in constructor for cases where Configure() is called before _Ready()
    }

    private void InitializeUI()
    {
        if (_service == null) return;
        if (_variantGrid != null) return; // Already initialized

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        // Header
        _headerLabel = new Label
        {
            Text = "Variant Mappings",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_headerLabel);

        // Info label
        _infoLabel = new Label
        {
            Text = "Configure atlas coordinates and sizes for each variant.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(_infoLabel);

        AddChild(new HSeparator());

        // Scroll container for variant grid
        _scrollContainer = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(_scrollContainer);

        // Variant grid
        _variantGrid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _variantGrid.AddThemeConstantOverride("h_separation", 8);
        _variantGrid.AddThemeConstantOverride("v_separation", 8);
        _scrollContainer.AddChild(_variantGrid);
    }

    /// <summary>
    /// Gets or sets whether the editor is read-only.
    /// </summary>
    public bool IsReadOnly
    {
        get => _isReadOnly;
        set
        {
            _isReadOnly = value;
            foreach (var cell in _variantCells.Values)
            {
                cell.SetReadOnly(value);
            }
        }
    }

    /// <summary>
    /// Configures the editor for a specific tile and format.
    /// </summary>
    public void Configure(EditableTile? tile, AutoTileFormatDefinition? format, bool readOnly = false)
    {
        _currentTile = tile;
        _currentFormat = format;
        _isReadOnly = readOnly;

        RebuildVariantGrid();
    }

    /// <summary>
    /// Refreshes the display for the current tile/format.
    /// </summary>
    public void Refresh()
    {
        if (_currentTile == null || _currentFormat == null)
        {
            ClearGrid();
            return;
        }

        RebuildVariantGrid();
    }

    /// <summary>
    /// Gets all variant definitions from the current UI state.
    /// </summary>
    public Dictionary<int, EditableVariantDefinition> GetVariantDefinitions()
    {
        var result = new Dictionary<int, EditableVariantDefinition>();

        foreach (var (bitmask, cell) in _variantCells)
        {
            result[bitmask] = cell.GetVariantDefinition();
        }

        return result;
    }

    private void ClearGrid()
    {
        if (_variantGrid == null) return;

        foreach (var child in _variantGrid.GetChildren())
        {
            child.QueueFree();
        }
        _variantCells.Clear();

        _infoLabel!.Text = "No tile selected or format not configured.";
    }

    private void RebuildVariantGrid()
    {
        ClearGrid();

        if (_currentTile == null || _currentFormat == null || _variantGrid == null)
        {
            return;
        }

        _infoLabel!.Text = $"Format: {_currentFormat.Name} ({_currentFormat.AllowedBitmasks.Count} variants)";

        // Create cells for each allowed bitmask
        var sortedBitmasks = _currentFormat.AllowedBitmasks.OrderBy(b => b).ToList();

        foreach (var bitmask in sortedBitmasks)
        {
            var cell = CreateVariantCell(bitmask);
            _variantGrid.AddChild(cell);
        }
    }

    private VBoxContainer CreateVariantCell(int bitmask)
    {
        var cell = new VariantCell(_service!, bitmask, _currentTile!, _currentFormat!);
        cell.VariantChanged += OnVariantCellChanged;
        cell.SetReadOnly(_isReadOnly);

        _variantCells[bitmask] = cell;
        return cell;
    }

    private void OnVariantCellChanged(int bitmask)
    {
        if (_isUpdating) return;

        EmitSignal(SignalName.VariantChanged, bitmask);
        EmitSignal(SignalName.VariantsModified);
    }

    /// <summary>
    /// Updates a specific variant from external source.
    /// </summary>
    public void SetVariant(int bitmask, EditableVariantDefinition definition)
    {
        if (_variantCells.TryGetValue(bitmask, out var cell))
        {
            _isUpdating = true;
            cell.SetVariantDefinition(definition);
            _isUpdating = false;
        }
    }
}

/// <summary>
/// Individual cell for editing a single variant's atlas coordinates and properties.
/// </summary>
[Tool]
public partial class VariantCell : VBoxContainer
{
    [Signal]
    public delegate void VariantChangedEventHandler(int bitmask);

    private readonly TileEditorService? _service;
    private readonly int _bitmask;
    private readonly EditableTile? _tile;
    private readonly AutoTileFormatDefinition? _format;

    // UI elements
    private TileShapePreview? _shapePreview;
    private Label? _bitmaskLabel;
    private Button? _atlasButton;
    private SpinBox? _sizeXSpin;
    private SpinBox? _sizeYSpin;
    private SpinBox? _offsetXSpin;
    private SpinBox? _offsetYSpin;
    private TextureRect? _variantPreview;
    private Label? _validationLabel;

    // Current variant state
    private EditableVariantDefinition _currentDefinition = new();
    private bool _isUpdating;
    private bool _isReadOnly;

    // Required by Godot for [Tool] classes
    public VariantCell() { }

    public VariantCell(TileEditorService service, int bitmask, EditableTile tile, AutoTileFormatDefinition format)
    {
        _service = service;
        _bitmask = bitmask;
        _tile = tile;
        _format = format;

        CustomMinimumSize = new Vector2(140, 200);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;

        BuildUI();
        LoadFromTile();
    }

    private void BuildUI()
    {
        // Bitmask shape preview
        _shapePreview = new TileShapePreview
        {
            CustomMinimumSize = new Vector2(30, 30),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        var previewFormat = _format?.BitmaskType switch
        {
            BitmaskType.Corner4 => TileShapePreview.Format.Corner16,
            BitmaskType.Edge4 => TileShapePreview.Format.Edge16,
            BitmaskType.Full8 => TileShapePreview.Format.Blob47,
            _ => TileShapePreview.Format.Corner16
        };
        _shapePreview.SetMask(_bitmask, previewFormat);
        AddChild(_shapePreview);

        // Bitmask label
        _bitmaskLabel = new Label
        {
            Text = $"Bitmask: {_bitmask}",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _bitmaskLabel.AddThemeFontSizeOverride("font_size", 10);
        AddChild(_bitmaskLabel);

        // Atlas coordinate button (opens picker dialog)
        _atlasButton = new Button
        {
            Text = "Atlas: (0, 0)",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Click to select atlas coordinates"
        };
        _atlasButton.Pressed += OnAtlasButtonPressed;
        AddChild(_atlasButton);

        // Size row
        var sizeRow = new HBoxContainer();
        sizeRow.AddChild(new Label { Text = "Size:", CustomMinimumSize = new Vector2(35, 0) });
        _sizeXSpin = new SpinBox { MinValue = 1, MaxValue = 8, Value = 1, CustomMinimumSize = new Vector2(50, 0) };
        _sizeXSpin.ValueChanged += OnSizeChanged;
        sizeRow.AddChild(_sizeXSpin);
        sizeRow.AddChild(new Label { Text = "x" });
        _sizeYSpin = new SpinBox { MinValue = 1, MaxValue = 8, Value = 1, CustomMinimumSize = new Vector2(50, 0) };
        _sizeYSpin.ValueChanged += OnSizeChanged;
        sizeRow.AddChild(_sizeYSpin);
        AddChild(sizeRow);

        // Offset row
        var offsetRow = new HBoxContainer();
        offsetRow.AddChild(new Label { Text = "Offset:", CustomMinimumSize = new Vector2(35, 0) });
        _offsetXSpin = new SpinBox { MinValue = -10, MaxValue = 10, Value = 0, CustomMinimumSize = new Vector2(50, 0) };
        _offsetXSpin.ValueChanged += OnOffsetChanged;
        offsetRow.AddChild(_offsetXSpin);
        offsetRow.AddChild(new Label { Text = "," });
        _offsetYSpin = new SpinBox { MinValue = -10, MaxValue = 10, Value = 0, CustomMinimumSize = new Vector2(50, 0) };
        _offsetYSpin.ValueChanged += OnOffsetChanged;
        offsetRow.AddChild(_offsetYSpin);
        AddChild(offsetRow);

        // Variant texture preview
        _variantPreview = new TextureRect
        {
            CustomMinimumSize = new Vector2(48, 48),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };
        AddChild(_variantPreview);

        // Validation label
        _validationLabel = new Label
        {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate = new Color(1f, 0.4f, 0.4f)
        };
        _validationLabel.AddThemeFontSizeOverride("font_size", 9);
        AddChild(_validationLabel);
    }

    private void LoadFromTile()
    {
        if (_tile == null) return;

        // Start with tile's definition (atlas coords, possibly custom size/offset)
        _currentDefinition = _tile.GetVariantDefinition(_bitmask);

        // If tile has no custom variant definition for this bitmask,
        // check the format's VariantMappings for Size defaults (format only stores size, not offset)
        var hasCustomDefinition = _tile.CustomVariantDefinitions?.ContainsKey(_bitmask) ?? false;
        if (!hasCustomDefinition && _format != null && _format.VariantMappings.TryGetValue(_bitmask, out var formatVariant))
        {
            // Use format's Size as default, but keep tile's atlas coords and offset defaults
            _currentDefinition.SizeX = formatVariant.Size.X > 0 ? formatVariant.Size.X : 1;
            _currentDefinition.SizeY = formatVariant.Size.Y > 0 ? formatVariant.Size.Y : 1;
            // Offset is tile-level only, default to 0,0
        }

        UpdateUIFromDefinition();
    }

    private void UpdateUIFromDefinition()
    {
        _isUpdating = true;

        _atlasButton!.Text = $"Atlas: ({_currentDefinition.AtlasX}, {_currentDefinition.AtlasY})";
        _sizeXSpin!.Value = _currentDefinition.SizeX;
        _sizeYSpin!.Value = _currentDefinition.SizeY;
        _offsetXSpin!.Value = _currentDefinition.OffsetX;
        _offsetYSpin!.Value = _currentDefinition.OffsetY;

        UpdatePreview();
        Validate();

        _isUpdating = false;
    }

    private void UpdatePreview()
    {
        if (_service == null || _tile == null || _variantPreview == null) return;

        var texture = _service.GetTileTexture(_tile);
        if (texture == null)
        {
            _variantPreview.Texture = null;
            return;
        }

        // Create atlas texture for the variant region
        var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        var actualTileSize = new Vector2I(
            (int)(tileSize.X / _tile.SourceScale),
            (int)(tileSize.Y / _tile.SourceScale)
        );

        var atlasTexture = new AtlasTexture
        {
            Atlas = texture,
            Region = new Rect2(
                _currentDefinition.AtlasX * actualTileSize.X,
                _currentDefinition.AtlasY * actualTileSize.Y,
                actualTileSize.X * _currentDefinition.SizeX,
                actualTileSize.Y * _currentDefinition.SizeY
            )
        };

        _variantPreview.Texture = atlasTexture;
    }

    private void Validate()
    {
        if (_service == null || _tile == null || _validationLabel == null) return;

        var texture = _service.GetTileTexture(_tile);
        if (texture == null)
        {
            _validationLabel.Text = "No texture";
            _validationLabel.Modulate = new Color(1f, 0.6f, 0.2f);
            return;
        }

        var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        var actualTileSize = new Vector2I(
            (int)(tileSize.X / _tile.SourceScale),
            (int)(tileSize.Y / _tile.SourceScale)
        );

        var regionRight = (_currentDefinition.AtlasX + _currentDefinition.SizeX) * actualTileSize.X;
        var regionBottom = (_currentDefinition.AtlasY + _currentDefinition.SizeY) * actualTileSize.Y;

        if (regionRight > texture.GetWidth() || regionBottom > texture.GetHeight())
        {
            _validationLabel.Text = "Out of bounds!";
            _validationLabel.Modulate = new Color(1f, 0.4f, 0.4f);
            TooltipText = $"Atlas region exceeds texture bounds ({texture.GetWidth()}x{texture.GetHeight()})";
        }
        else
        {
            _validationLabel.Text = "";
            TooltipText = $"Bitmask {_bitmask}: {_currentDefinition.SizeX}x{_currentDefinition.SizeY} at ({_currentDefinition.AtlasX}, {_currentDefinition.AtlasY})";
        }
    }

    private void OnAtlasButtonPressed()
    {
        if (_isReadOnly) return;

        // For now, show a simple dialog. Full atlas picker integration comes in ST004/ST005.
        var dialog = new AcceptDialog
        {
            Title = $"Set Atlas Coords (Bitmask {_bitmask})",
            Size = new Vector2I(300, 150)
        };

        var vbox = new VBoxContainer();

        var xRow = new HBoxContainer();
        xRow.AddChild(new Label { Text = "X:", CustomMinimumSize = new Vector2(30, 0) });
        var xSpin = new SpinBox { MinValue = 0, MaxValue = 99, Value = _currentDefinition.AtlasX, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        xRow.AddChild(xSpin);
        vbox.AddChild(xRow);

        var yRow = new HBoxContainer();
        yRow.AddChild(new Label { Text = "Y:", CustomMinimumSize = new Vector2(30, 0) });
        var ySpin = new SpinBox { MinValue = 0, MaxValue = 99, Value = _currentDefinition.AtlasY, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        yRow.AddChild(ySpin);
        vbox.AddChild(yRow);

        dialog.AddChild(vbox);

        dialog.Confirmed += () =>
        {
            _currentDefinition.AtlasX = (int)xSpin.Value;
            _currentDefinition.AtlasY = (int)ySpin.Value;
            UpdateUIFromDefinition();
            EmitVariantChanged();
            dialog.QueueFree();
        };

        dialog.Canceled += () => dialog.QueueFree();

        GetTree().Root.AddChild(dialog);
        dialog.PopupCentered();
    }

    private void OnSizeChanged(double value)
    {
        if (_isUpdating || _isReadOnly) return;

        _currentDefinition.SizeX = (int)_sizeXSpin!.Value;
        _currentDefinition.SizeY = (int)_sizeYSpin!.Value;
        UpdatePreview();
        Validate();
        EmitVariantChanged();
    }

    private void OnOffsetChanged(double value)
    {
        if (_isUpdating || _isReadOnly) return;

        _currentDefinition.OffsetX = (int)_offsetXSpin!.Value;
        _currentDefinition.OffsetY = (int)_offsetYSpin!.Value;
        EmitVariantChanged();
    }

    private void EmitVariantChanged()
    {
        EmitSignal(SignalName.VariantChanged, _bitmask);
    }

    public void SetReadOnly(bool readOnly)
    {
        _isReadOnly = readOnly;
        _atlasButton!.Disabled = readOnly;
        _sizeXSpin!.Editable = !readOnly;
        _sizeYSpin!.Editable = !readOnly;
        _offsetXSpin!.Editable = !readOnly;
        _offsetYSpin!.Editable = !readOnly;
    }

    public EditableVariantDefinition GetVariantDefinition()
    {
        return _currentDefinition.Clone();
    }

    public void SetVariantDefinition(EditableVariantDefinition definition)
    {
        _currentDefinition = definition.Clone();
        UpdateUIFromDefinition();
    }
}
#endif
