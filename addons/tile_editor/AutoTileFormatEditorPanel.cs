#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
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

    public override void _Ready()
    {
        if (_service == null) return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SplitOffset = 200;

        SetupLeftPanel();
        SetupRightPanel();

        // Subscribe to service events
        _service.TilesLoaded += OnTilesLoaded;
        _service.AutoTileFormatsLoaded += OnAutoTileFormatsLoaded;

        // Initial population
        CallDeferred(MethodName.PopulateFormatList);
        CallDeferred(MethodName.LoadCustomFormatsFromService);
    }

    private void SetupLeftPanel()
    {
        var leftVBox = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(180, 0)
        };
        AddChild(leftVBox);

        // Header
        var header = new Label
        {
            Text = "Auto-Tile Formats",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 14);
        leftVBox.AddChild(header);

        leftVBox.AddChild(new HSeparator());

        // Format list
        _formatList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _formatList.ItemSelected += OnFormatSelected;
        leftVBox.AddChild(_formatList);

        // Buttons
        var buttonRow = new HBoxContainer();

        var addButton = new Button
        {
            Text = "+ Add",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Create a new custom auto-tile format"
        };
        addButton.Pressed += OnAddPressed;
        buttonRow.AddChild(addButton);

        _deleteButton = new Button
        {
            Text = "Delete",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Delete the selected custom format",
            Disabled = true
        };
        _deleteButton.Pressed += OnDeletePressed;
        buttonRow.AddChild(_deleteButton);

        leftVBox.AddChild(buttonRow);
    }

    private void SetupRightPanel()
    {
        _detailsPanel = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_detailsPanel);

        // No selection label
        _noSelectionLabel = new Label
        {
            Text = "Select a format to view details",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _detailsPanel.AddChild(_noSelectionLabel);

        // Format details container (hidden by default)
        _formatDetailsContainer = new VBoxContainer
        {
            Visible = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _detailsPanel.AddChild(_formatDetailsContainer);

        // Name field
        var nameRow = CreateRow("Name:");
        _nameField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Editable = false
        };
        _nameField.TextChanged += OnNameChanged;
        nameRow.AddChild(_nameField);
        _formatDetailsContainer.AddChild(nameRow);

        // Bitmask type dropdown
        var typeRow = CreateRow("Bitmask Type:");
        _bitmaskTypeDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _bitmaskTypeDropdown.AddItem("Corner4 (4-bit corners)", (int)BitmaskType.Corner4);
        _bitmaskTypeDropdown.AddItem("Edge4 (4-bit edges)", (int)BitmaskType.Edge4);
        _bitmaskTypeDropdown.AddItem("Full8 (8-bit blob)", (int)BitmaskType.Full8);
        _bitmaskTypeDropdown.ItemSelected += OnBitmaskTypeChanged;
        typeRow.AddChild(_bitmaskTypeDropdown);
        _formatDetailsContainer.AddChild(typeRow);

        // Variant count
        var countRow = CreateRow("Variant Count:");
        _variantCountLabel = new Label { Text = "0" };
        countRow.AddChild(_variantCountLabel);
        _formatDetailsContainer.AddChild(countRow);

        // Built-in indicator
        var builtInNote = new Label
        {
            Text = "",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        builtInNote.AddThemeFontSizeOverride("font_size", 11);
        builtInNote.Name = "BuiltInNote";
        _formatDetailsContainer.AddChild(builtInNote);

        _formatDetailsContainer.AddChild(new HSeparator());

        // Allowed Bitmasks section
        var bitmaskHeader = new Label { Text = "Allowed Bitmasks" };
        bitmaskHeader.AddThemeFontSizeOverride("font_size", 14);
        _formatDetailsContainer.AddChild(bitmaskHeader);

        var bitmaskInfo = new Label
        {
            Text = "Toggle which bitmask patterns are valid for this format:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        bitmaskInfo.AddThemeFontSizeOverride("font_size", 11);
        _formatDetailsContainer.AddChild(bitmaskInfo);

        // Select all / Deselect all buttons
        var selectRow = new HBoxContainer();
        var selectAllBtn = new Button { Text = "Select All" };
        selectAllBtn.Pressed += () => _bitmaskGrid?.SelectAll();
        selectRow.AddChild(selectAllBtn);

        var deselectAllBtn = new Button { Text = "Deselect All" };
        deselectAllBtn.Pressed += () => _bitmaskGrid?.DeselectAll();
        selectRow.AddChild(deselectAllBtn);
        _formatDetailsContainer.AddChild(selectRow);

        // Bitmask grid in scroll container
        _bitmaskScrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };

        _bitmaskGrid = new BitmaskConfigGrid();
        _bitmaskGrid.AllowedBitmasksChanged += OnAllowedBitmasksChanged;
        _bitmaskScrollContainer.AddChild(_bitmaskGrid);
        _formatDetailsContainer.AddChild(_bitmaskScrollContainer);
    }

    private void OnTilesLoaded()
    {
        PopulateFormatList();
    }

    private void OnAutoTileFormatsLoaded()
    {
        LoadCustomFormatsFromService();
        PopulateFormatList();
    }

    private void LoadCustomFormatsFromService()
    {
        if (_service == null) return;

        _customFormats.Clear();
        foreach (var format in _service.CustomAutoTileFormats)
        {
            _customFormats[format.Name] = new EditableAutoTileFormat
            {
                Name = format.Name,
                BitmaskType = format.BitmaskType,
                AllowedBitmasks = new HashSet<int>(format.AllowedBitmasks)
            };
        }
    }

    private void PopulateFormatList()
    {
        _formatList?.Clear();

        // Get all formats from registry
        var formats = AutoTileFormatRegistry.GetAll().OrderBy(f => f.IsBuiltIn ? 0 : 1).ThenBy(f => f.Name);

        foreach (var format in formats)
        {
            var displayName = format.IsBuiltIn
                ? $"{format.Name} (built-in)"
                : format.Name;

            var idx = _formatList!.AddItem(displayName);
            _formatList.SetItemMetadata(idx, format.Name);

            // Grey out built-in formats slightly
            if (format.IsBuiltIn)
            {
                _formatList.SetItemCustomFgColor(idx, new Color(0.7f, 0.7f, 0.7f));
            }
        }
    }

    private void OnFormatSelected(long index)
    {
        var formatName = _formatList!.GetItemMetadata((int)index).AsString();
        SelectFormat(formatName);
    }

    private void SelectFormat(string formatName)
    {
        _selectedFormatName = formatName;

        if (!AutoTileFormatRegistry.TryGet(formatName, out var format) || format == null)
        {
            _selectedFormat = null;
            ShowNoSelection();
            return;
        }

        _selectedFormat = format;
        ShowFormatDetails();
    }

    private void ShowNoSelection()
    {
        _noSelectionLabel!.Visible = true;
        _formatDetailsContainer!.Visible = false;
        _deleteButton!.Disabled = true;
    }

    private void ShowFormatDetails()
    {
        if (_selectedFormat == null) return;

        _isUpdating = true;

        _noSelectionLabel!.Visible = false;
        _formatDetailsContainer!.Visible = true;

        // Populate fields
        _nameField!.Text = _selectedFormat.Name;
        _nameField.Editable = !_selectedFormat.IsBuiltIn;

        // Set bitmask type dropdown
        _bitmaskTypeDropdown!.Selected = _selectedFormat.BitmaskType switch
        {
            BitmaskType.Corner4 => 0,
            BitmaskType.Edge4 => 1,
            BitmaskType.Full8 => 2,
            _ => 0
        };
        _bitmaskTypeDropdown.Disabled = _selectedFormat.IsBuiltIn;

        // Update variant count
        _variantCountLabel!.Text = _selectedFormat.AllowedBitmasks.Count.ToString();

        // Update built-in note
        var builtInNote = _formatDetailsContainer.GetNode<Label>("BuiltInNote");
        if (builtInNote != null)
        {
            builtInNote.Text = _selectedFormat.IsBuiltIn
                ? "(Built-in format - cannot be modified)"
                : "(Custom format - editable)";
            builtInNote.Modulate = _selectedFormat.IsBuiltIn
                ? new Color(1f, 0.8f, 0.4f)
                : new Color(0.4f, 0.8f, 0.4f);
        }

        // Configure bitmask grid
        _bitmaskGrid!.Configure(
            _selectedFormat.BitmaskType,
            _selectedFormat.AllowedBitmasks,
            _selectedFormat.IsBuiltIn
        );

        // Enable/disable delete button
        _deleteButton!.Disabled = _selectedFormat.IsBuiltIn;

        _isUpdating = false;
    }

    private void OnNameChanged(string newName)
    {
        if (_isUpdating || _selectedFormat == null || _selectedFormat.IsBuiltIn) return;

        // For custom formats, track the name change
        // Note: Renaming would require unregistering and re-registering, which is complex
        // For simplicity, names are immutable after creation
    }

    private void OnBitmaskTypeChanged(long index)
    {
        if (_isUpdating || _selectedFormat == null || _selectedFormat.IsBuiltIn) return;

        var newType = index switch
        {
            1 => BitmaskType.Edge4,
            2 => BitmaskType.Full8,
            _ => BitmaskType.Corner4
        };

        // Update the format
        UpdateCustomFormat(_selectedFormat.Name, newType, null);
    }

    private void OnAllowedBitmasksChanged()
    {
        if (_isUpdating || _selectedFormat == null || _selectedFormat.IsBuiltIn) return;
        if (_bitmaskGrid == null) return;

        // Update the format with new allowed bitmasks
        UpdateCustomFormat(_selectedFormat.Name, null, _bitmaskGrid.AllowedBitmasks.ToHashSet());

        // Update variant count display
        _variantCountLabel!.Text = _bitmaskGrid.AllowedBitmasks.Count.ToString();
    }

    private void UpdateCustomFormat(string name, BitmaskType? newType, HashSet<int>? newAllowedBitmasks)
    {
        if (!_customFormats.TryGetValue(name, out var editableFormat))
        {
            // This shouldn't happen for custom formats, but guard anyway
            return;
        }

        if (newType.HasValue)
        {
            editableFormat.BitmaskType = newType.Value;
            // When type changes, reset allowed bitmasks to all valid for that type
            editableFormat.AllowedBitmasks = AutoTileFormatDefinition.AllBitmasksFor(newType.Value);
            _bitmaskGrid?.Configure(newType.Value, editableFormat.AllowedBitmasks, false);
        }

        if (newAllowedBitmasks != null)
        {
            editableFormat.AllowedBitmasks = newAllowedBitmasks;
        }

        // Re-register the updated format
        ReregisterCustomFormat(editableFormat);

        EmitSignal(SignalName.FormatModified, name);
    }

    private void ReregisterCustomFormat(EditableAutoTileFormat editableFormat)
    {
        // Unregister old version
        AutoTileFormatRegistry.Unregister(editableFormat.Name);

        // Create new definition
        var newDefinition = new AutoTileFormatDefinition(
            editableFormat.Name,
            editableFormat.BitmaskType,
            editableFormat.AllowedBitmasks,
            CreateDefaultVariantMappings(editableFormat.BitmaskType, editableFormat.AllowedBitmasks),
            isBuiltIn: false
        );

        // Register new version
        AutoTileFormatRegistry.Register(newDefinition);

        // Update selected format reference
        if (_selectedFormatName == editableFormat.Name)
        {
            AutoTileFormatRegistry.TryGet(editableFormat.Name, out _selectedFormat);
        }
    }

    private void OnAddPressed()
    {
        if (_newFormatDialog == null)
        {
            CreateNewFormatDialog();
        }

        // Reset fields
        _newFormatNameField!.Text = "";
        _newFormatTypeDropdown!.Selected = 0;
        _newFormatDialog!.Popup();
    }

    private void CreateNewFormatDialog()
    {
        _newFormatDialog = new AcceptDialog
        {
            Title = "Create New Auto-Tile Format",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
            Size = new Vector2I(400, 200),
            OkButtonText = "Create"
        };

        var vbox = new VBoxContainer();

        // Name field
        var nameRow = CreateRow("Name:");
        _newFormatNameField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "my_custom_format"
        };
        nameRow.AddChild(_newFormatNameField);
        vbox.AddChild(nameRow);

        var nameNote = new Label
        {
            Text = "Use snake_case, e.g., hedge4, wall_south",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        nameNote.AddThemeFontSizeOverride("font_size", 10);
        vbox.AddChild(nameNote);

        // Type dropdown
        var typeRow = CreateRow("Bitmask Type:");
        _newFormatTypeDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _newFormatTypeDropdown.AddItem("Corner4 (16 variants)", 0);
        _newFormatTypeDropdown.AddItem("Edge4 (16 variants)", 1);
        _newFormatTypeDropdown.AddItem("Full8 (47 variants)", 2);
        typeRow.AddChild(_newFormatTypeDropdown);
        vbox.AddChild(typeRow);

        _newFormatDialog.AddChild(vbox);
        AddChild(_newFormatDialog);

        _newFormatDialog.Confirmed += OnNewFormatConfirmed;
    }

    private void OnNewFormatConfirmed()
    {
        var name = _newFormatNameField!.Text.Trim().ToLowerInvariant();

        // Validate name
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Name cannot be empty");
            return;
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-z][a-z0-9_]*$"))
        {
            ShowError("Name must be snake_case (lowercase letters, numbers, underscores)");
            return;
        }

        if (AutoTileFormatRegistry.Contains(name))
        {
            ShowError($"A format named '{name}' already exists");
            return;
        }

        // Create the format
        var bitmaskType = _newFormatTypeDropdown!.Selected switch
        {
            1 => BitmaskType.Edge4,
            2 => BitmaskType.Full8,
            _ => BitmaskType.Corner4
        };

        var allowedBitmasks = AutoTileFormatDefinition.AllBitmasksFor(bitmaskType);
        var variantMappings = CreateDefaultVariantMappings(bitmaskType, allowedBitmasks);

        var newFormat = new AutoTileFormatDefinition(
            name,
            bitmaskType,
            allowedBitmasks,
            variantMappings,
            isBuiltIn: false
        );

        // Register it
        if (AutoTileFormatRegistry.Register(newFormat))
        {
            // Track in custom formats
            _customFormats[name] = new EditableAutoTileFormat
            {
                Name = name,
                BitmaskType = bitmaskType,
                AllowedBitmasks = allowedBitmasks
            };

            PopulateFormatList();
            EmitSignal(SignalName.FormatCreated, name);

            // Select the new format
            for (int i = 0; i < _formatList!.ItemCount; i++)
            {
                if (_formatList.GetItemMetadata(i).AsString() == name)
                {
                    _formatList.Select(i);
                    SelectFormat(name);
                    break;
                }
            }
        }
    }

    private void OnDeletePressed()
    {
        if (_selectedFormat == null || _selectedFormat.IsBuiltIn) return;

        var dialog = new ConfirmationDialog
        {
            DialogText = $"Delete format '{_selectedFormat.Name}'?\n\nThis cannot be undone.",
            OkButtonText = "Delete"
        };
        dialog.Confirmed += () =>
        {
            var name = _selectedFormat.Name;

            // Unregister
            if (AutoTileFormatRegistry.Unregister(name))
            {
                _customFormats.Remove(name);
                _selectedFormat = null;
                _selectedFormatName = null;

                PopulateFormatList();
                ShowNoSelection();

                EmitSignal(SignalName.FormatDeleted, name);
            }

            dialog.QueueFree();
        };
        dialog.Canceled += () => dialog.QueueFree();

        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void ShowError(string message)
    {
        var dialog = new AcceptDialog
        {
            Title = "Error",
            DialogText = message
        };
        dialog.Confirmed += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private static Dictionary<int, VariantDefinition> CreateDefaultVariantMappings(
        BitmaskType type, HashSet<int> allowedBitmasks)
    {
        // Create default 1x1 variant mappings for all allowed bitmasks
        // Atlas coordinates will need to be configured per-tile
        var mappings = new Dictionary<int, VariantDefinition>();

        foreach (var bitmask in allowedBitmasks)
        {
            // Default to atlas (0,0) - actual coords configured per-tile
            mappings[bitmask] = new VariantDefinition(Vector2I.Zero);
        }

        return mappings;
    }

    private static HBoxContainer CreateRow(string label)
    {
        var row = new HBoxContainer();
        var lbl = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(100, 0)
        };
        row.AddChild(lbl);
        return row;
    }

    /// <summary>
    /// Gets all custom (non-built-in) formats for serialization.
    /// </summary>
    public IEnumerable<EditableAutoTileFormat> GetCustomFormats()
    {
        return _customFormats.Values;
    }

    /// <summary>
    /// Loads custom formats from saved data and registers them.
    /// </summary>
    public void LoadCustomFormats(IEnumerable<EditableAutoTileFormat> formats)
    {
        foreach (var format in formats)
        {
            // Skip if already registered (shouldn't happen but be safe)
            if (AutoTileFormatRegistry.Contains(format.Name))
                continue;

            var variantMappings = CreateDefaultVariantMappings(format.BitmaskType, format.AllowedBitmasks);

            var definition = new AutoTileFormatDefinition(
                format.Name,
                format.BitmaskType,
                format.AllowedBitmasks,
                variantMappings,
                isBuiltIn: false
            );

            if (AutoTileFormatRegistry.Register(definition))
            {
                _customFormats[format.Name] = format;
            }
        }

        PopulateFormatList();
    }

    /// <summary>
    /// Refreshes the panel after external changes.
    /// </summary>
    public void Refresh()
    {
        PopulateFormatList();
        if (_selectedFormatName != null)
        {
            SelectFormat(_selectedFormatName);
        }
    }
}

/// <summary>
/// Mutable format data for custom formats being edited.
/// </summary>
public class EditableAutoTileFormat
{
    public string Name { get; set; } = "";
    public BitmaskType BitmaskType { get; set; } = BitmaskType.Corner4;
    public HashSet<int> AllowedBitmasks { get; set; } = new();
}
#endif
