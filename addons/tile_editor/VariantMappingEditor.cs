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

    private EditableTile? _currentTile;
    private AutoTileFormatDefinition? _currentFormat;
    private bool _isUpdating;
    private bool _isReadOnly;

    private readonly Dictionary<int, VariantCell> _variantCells = new();

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
        if (_variantGrid != null) return;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;

        _headerLabel = new Label
        {
            Text = "Variant Mappings",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        _headerLabel.AddThemeFontSizeOverride("font_size", 14);
        AddChild(_headerLabel);

        _infoLabel = new Label
        {
            Text = "Configure atlas coordinates and sizes for each variant.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(_infoLabel);

        AddChild(new HSeparator());

        _scrollContainer = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        AddChild(_scrollContainer);

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
#endif
