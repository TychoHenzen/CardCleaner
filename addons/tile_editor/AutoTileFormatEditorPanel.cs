#if TOOLS
using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for viewing, creating, and editing auto-tile format definitions.
/// Built-in formats are shown but cannot be modified or deleted.
/// </summary>
[Tool]
public partial class AutoTileFormatEditorPanel : HSplitContainer
{
    [Signal]
    public delegate void FormatCreatedEventHandler(string formatName);

    [Signal]
    public delegate void FormatDeletedEventHandler(string formatName);

    [Signal]
    public delegate void FormatModifiedEventHandler(string formatName);

    private readonly TileEditorService? _service;
    private ItemList? _formatList;
    private VBoxContainer? _detailsPanel;
    private Label? _noSelectionLabel;
    private VBoxContainer? _formatDetailsContainer;

    // Format details controls
    private LineEdit? _nameField;
    private OptionButton? _bitmaskTypeDropdown;
    private Label? _variantCountLabel;
    private BitmaskConfigGrid? _bitmaskGrid;
    private Button? _deleteButton;
    private ScrollContainer? _bitmaskScrollContainer;

    // Variant configuration controls
    private FoldoutContainer? _variantConfigFoldout;
    private VBoxContainer? _variantConfigContainer;
    private readonly Dictionary<int, VariantConfigRow> _variantConfigRows = new();

    // Current state
    private string? _selectedFormatName;
    private AutoTileFormatDefinition? _selectedFormat;
    private bool _isUpdating;

    // For creating new formats
    private AcceptDialog? _newFormatDialog;
    private LineEdit? _newFormatNameField;
    private OptionButton? _newFormatTypeDropdown;

    // Mutable format data for custom formats being edited
    private readonly Dictionary<string, EditableAutoTileFormat> _customFormats = new();

    // Required by Godot for [Tool] classes
    public AutoTileFormatEditorPanel() { }

    public AutoTileFormatEditorPanel(TileEditorService service)
    {
        _service = service;
    }

}

#endif
