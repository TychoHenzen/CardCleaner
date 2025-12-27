#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Panel for editing tile properties
/// </summary>
[Tool]
public partial class TilePropertiesPanel : ScrollContainer
{
    private readonly TileEditorService _service;
    private string? _selectedTileId;
    private EditableTile? _currentTile;

    // Atlas picker controls
    private TilesetAtlasPicker? _atlasPicker;
    private OptionButton? _sourceDropdown;

    // Form fields
    private LineEdit? _idField;
    private LineEdit? _nameField;
    private OptionButton? _passabilityField;
    private SpinBox? _atlasXField;
    private SpinBox? _atlasYField;
    private SpinBox? _sourceIdField;
    private OptionButton? _layerField;
    private SpinBox? _elevationField;
    private CheckBox? _transparentField;
    private SpinBox? _sizeXField;
    private SpinBox? _sizeYField;
    private VBoxContainer? _biomesContainer;
    private readonly Dictionary<string, CheckBox> _biomeCheckboxes = new();
    private Label? _validationLabel;

    private bool _isUpdating;

    public TilePropertiesPanel(TileEditorService service)
    {
        _service = service;
    }

    public override void _Ready()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;

        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(vbox);

        // Header
        vbox.AddChild(new Label
        {
            Text = "Select a tile to edit its properties",
            HorizontalAlignment = HorizontalAlignment.Center
        });

        vbox.AddChild(new HSeparator());

        // ID field (read-only once created)
        var idRow = CreateRow("ID:");
        _idField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "tile_id (snake_case)"
        };
        _idField.TextChanged += OnFieldChanged;
        idRow.AddChild(_idField);
        vbox.AddChild(idRow);

        // Name field
        var nameRow = CreateRow("Name:");
        _nameField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "Display Name"
        };
        _nameField.TextChanged += OnFieldChanged;
        nameRow.AddChild(_nameField);
        vbox.AddChild(nameRow);

        // Passability dropdown
        var passRow = CreateRow("Passability:");
        _passabilityField = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _passabilityField.AddItem("Passable", 0);
        _passabilityField.AddItem("Solid", 1);
        _passabilityField.AddItem("Partially Passable", 2);
        _passabilityField.ItemSelected += _ => OnFieldChanged("");
        passRow.AddChild(_passabilityField);
        vbox.AddChild(passRow);

        vbox.AddChild(new HSeparator());

        // Atlas selection section
        var atlasHeader = new Label { Text = "Atlas Coordinates" };
        atlasHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(atlasHeader);

        // Source dropdown
        var sourceRow = CreateRow("Source:");
        _sourceDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _sourceDropdown.ItemSelected += OnSourceDropdownChanged;
        sourceRow.AddChild(_sourceDropdown);
        vbox.AddChild(sourceRow);

        // Atlas picker in scroll container
        var pickerLabel = new Label
        {
            Text = "Click to select tile:",
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        pickerLabel.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(pickerLabel);

        var pickerScroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(0, 150),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto
        };
        _atlasPicker = new TilesetAtlasPicker();
        _atlasPicker.TileSelected += OnPickerTileSelected;
        pickerScroll.AddChild(_atlasPicker);
        vbox.AddChild(pickerScroll);

        // Manual coordinate entry
        var coordsLabel = new Label
        {
            Text = "Or enter manually:",
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        coordsLabel.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(coordsLabel);

        // Atlas coordinates
        var atlasRow = CreateRow("Atlas Coords:");
        var atlasHBox = new HBoxContainer();
        atlasHBox.AddChild(new Label { Text = "X:" });
        _atlasXField = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _atlasXField.ValueChanged += _ => OnFieldChanged("");
        atlasHBox.AddChild(_atlasXField);
        atlasHBox.AddChild(new Label { Text = "Y:" });
        _atlasYField = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _atlasYField.ValueChanged += _ => OnFieldChanged("");
        atlasHBox.AddChild(_atlasYField);
        atlasRow.AddChild(atlasHBox);
        vbox.AddChild(atlasRow);

        // Hidden source ID field for internal tracking
        _sourceIdField = new SpinBox { Visible = false, Value = 4 };
        AddChild(_sourceIdField);

        vbox.AddChild(new HSeparator());

        // Layer dropdown
        var layerRow = CreateRow("Layer:");
        _layerField = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _layerField.AddItem("Terrain", 0);
        _layerField.AddItem("Decoration", 1);
        _layerField.AddItem("Structure", 2);
        _layerField.AddItem("Effects", 3);
        _layerField.ItemSelected += _ => OnFieldChanged("");
        layerRow.AddChild(_layerField);
        vbox.AddChild(layerRow);

        // Elevation
        var elevRow = CreateRow("Elevation:");
        _elevationField = new SpinBox
        {
            MinValue = -10,
            MaxValue = 10,
            Step = 0.1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _elevationField.ValueChanged += _ => OnFieldChanged("");
        elevRow.AddChild(_elevationField);
        vbox.AddChild(elevRow);

        // Transparent
        var transRow = CreateRow("Transparent:");
        _transparentField = new CheckBox();
        _transparentField.Toggled += _ => OnFieldChanged("");
        transRow.AddChild(_transparentField);
        vbox.AddChild(transRow);

        vbox.AddChild(new HSeparator());

        // Size section (for multi-tile objects like trees)
        var sizeHeader = new Label { Text = "Tile Size (cells)" };
        sizeHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(sizeHeader);

        var sizeRow = CreateRow("Size:");
        var sizeHBox = new HBoxContainer();
        sizeHBox.AddChild(new Label { Text = "W:" });
        _sizeXField = new SpinBox
        {
            MinValue = 1,
            MaxValue = 10,
            Step = 1,
            Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sizeXField.ValueChanged += _ => OnFieldChanged("");
        sizeHBox.AddChild(_sizeXField);
        sizeHBox.AddChild(new Label { Text = "H:" });
        _sizeYField = new SpinBox
        {
            MinValue = 1,
            MaxValue = 10,
            Step = 1,
            Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sizeYField.ValueChanged += _ => OnFieldChanged("");
        sizeHBox.AddChild(_sizeYField);
        sizeRow.AddChild(sizeHBox);
        vbox.AddChild(sizeRow);

        var sizeNote = new Label
        {
            Text = "(For multi-cell tiles like trees: 2x2, etc.)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        sizeNote.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(sizeNote);

        vbox.AddChild(new HSeparator());

        // Biomes section
        vbox.AddChild(new Label { Text = "Allowed Biomes:" });
        _biomesContainer = new VBoxContainer();
        vbox.AddChild(_biomesContainer);

        var biomes = new[] { "plains", "forest", "desert", "tundra", "swamp", "mountains" };
        foreach (var biome in biomes)
        {
            var checkbox = new CheckBox
            {
                Text = char.ToUpper(biome[0]) + biome[1..],
                ButtonPressed = false
            };
            checkbox.Toggled += _ => OnFieldChanged("");
            _biomesContainer.AddChild(checkbox);
            _biomeCheckboxes[biome] = checkbox;
        }

        var biomeNote = new Label
        {
            Text = "(Leave all unchecked for universal tile)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        biomeNote.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(biomeNote);

        vbox.AddChild(new HSeparator());

        // Validation message
        _validationLabel = new Label
        {
            Text = "",
            Modulate = new Color(1, 0.3f, 0.3f)
        };
        vbox.AddChild(_validationLabel);

        // Initially disable all fields
        SetFieldsEnabled(false);
    }

    private void PopulateSourceDropdown()
    {
        _sourceDropdown!.Clear();
        var sources = _service.GetAvailableAtlasSources();

        foreach (var sourceInfo in sources)
        {
            _sourceDropdown.AddItem(sourceInfo.DisplayName, sourceInfo.SourceId);
        }
    }

    private void OnSourceDropdownChanged(long index)
    {
        if (_isUpdating || _sourceDropdown == null) return;

        var sourceId = _sourceDropdown.GetItemId((int)index);
        _sourceIdField!.Value = sourceId;

        // Reload picker with new source
        var source = _service.GetAtlasSource(sourceId);
        if (source != null && _atlasPicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _atlasPicker.SetSource(source, tileSize, sourceId);
        }

        OnFieldChanged("");
    }

    private void OnPickerTileSelected(Vector2I atlasCoords, int sourceId)
    {
        if (_isUpdating || _currentTile == null) return;

        _isUpdating = true;
        _atlasXField!.Value = atlasCoords.X;
        _atlasYField!.Value = atlasCoords.Y;
        _sourceIdField!.Value = sourceId;
        _isUpdating = false;

        OnFieldChanged("");
    }

    public void SelectTile(string tileId)
    {
        _selectedTileId = tileId;
        _currentTile = _service.GetTile(tileId)?.Clone();

        if (_currentTile == null)
        {
            SetFieldsEnabled(false);
            return;
        }

        PopulateSourceDropdown();
        SetFieldsEnabled(true);
        PopulateFields();
    }

    private void PopulateFields()
    {
        if (_currentTile == null) return;

        _isUpdating = true;

        _idField!.Text = _currentTile.Id;
        _idField.Editable = false; // ID is immutable after creation
        _nameField!.Text = _currentTile.Name;

        _passabilityField!.Selected = _currentTile.Passability.ToLowerInvariant() switch
        {
            "solid" => 1,
            "partially_passable" => 2,
            _ => 0
        };

        // Sync source dropdown
        for (int i = 0; i < _sourceDropdown!.ItemCount; i++)
        {
            if (_sourceDropdown.GetItemId(i) == _currentTile.SourceId)
            {
                _sourceDropdown.Selected = i;
                break;
            }
        }

        // Load atlas picker
        var source = _service.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _atlasPicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _atlasPicker.SetSource(source, tileSize, _currentTile.SourceId);
            _atlasPicker.SelectedCoords = new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
            _atlasPicker.SelectedSize = new Vector2I(_currentTile.SizeX, _currentTile.SizeY);
        }

        _atlasXField!.Value = _currentTile.AtlasX;
        _atlasYField!.Value = _currentTile.AtlasY;
        _sourceIdField!.Value = _currentTile.SourceId;

        _layerField!.Selected = _currentTile.Layer.ToLowerInvariant() switch
        {
            "decoration" => 1,
            "structure" => 2,
            "effects" => 3,
            _ => 0
        };

        _elevationField!.Value = _currentTile.Elevation;
        _transparentField!.ButtonPressed = _currentTile.IsTransparent;
        _sizeXField!.Value = _currentTile.SizeX;
        _sizeYField!.Value = _currentTile.SizeY;

        // Update biome checkboxes
        foreach (var (biome, checkbox) in _biomeCheckboxes)
        {
            checkbox.ButtonPressed = _currentTile.Biomes.Contains(biome, StringComparer.OrdinalIgnoreCase);
        }

        _validationLabel!.Text = "";
        _isUpdating = false;
    }

    private void OnFieldChanged(string _)
    {
        if (_isUpdating || _currentTile == null) return;

        // Update current tile from fields
        _currentTile.Name = _nameField!.Text;

        _currentTile.Passability = _passabilityField!.Selected switch
        {
            1 => "solid",
            2 => "partially_passable",
            _ => "passable"
        };

        _currentTile.AtlasX = (int)_atlasXField!.Value;
        _currentTile.AtlasY = (int)_atlasYField!.Value;
        _currentTile.SourceId = (int)_sourceIdField!.Value;

        // Sync picker coordinates if changed via spinbox
        if (_atlasPicker != null)
        {
            var newCoords = new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
            if (_atlasPicker.SelectedCoords != newCoords)
            {
                _isUpdating = true;
                _atlasPicker.SelectedCoords = newCoords;
                _isUpdating = false;
            }
        }

        _currentTile.Layer = _layerField!.Selected switch
        {
            1 => "decoration",
            2 => "structure",
            3 => "effects",
            _ => "terrain"
        };

        _currentTile.Elevation = (float)_elevationField!.Value;
        _currentTile.IsTransparent = _transparentField!.ButtonPressed;
        _currentTile.SizeX = (int)_sizeXField!.Value;
        _currentTile.SizeY = (int)_sizeYField!.Value;

        // Sync picker size when size fields change
        if (_atlasPicker != null)
        {
            var newSize = new Vector2I(_currentTile.SizeX, _currentTile.SizeY);
            if (_atlasPicker.SelectedSize != newSize)
            {
                _atlasPicker.SelectedSize = newSize;
            }
        }

        // Update biomes
        _currentTile.Biomes.Clear();
        foreach (var (biome, checkbox) in _biomeCheckboxes)
        {
            if (checkbox.ButtonPressed)
            {
                _currentTile.Biomes.Add(biome);
            }
        }

        // Validate
        var (valid, message) = _service.ValidateTile(_currentTile);
        _validationLabel!.Text = valid ? "" : message;

        if (valid)
        {
            // Push changes to service
            _service.UpdateTile(_currentTile);
        }
    }

    private void SetFieldsEnabled(bool enabled)
    {
        _idField!.Editable = false; // Always read-only
        _nameField!.Editable = enabled;
        _passabilityField!.Disabled = !enabled;
        _sourceDropdown!.Disabled = !enabled;
        _atlasXField!.Editable = enabled;
        _atlasYField!.Editable = enabled;
        _layerField!.Disabled = !enabled;
        _elevationField!.Editable = enabled;
        _transparentField!.Disabled = !enabled;
        _sizeXField!.Editable = enabled;
        _sizeYField!.Editable = enabled;

        foreach (var checkbox in _biomeCheckboxes.Values)
        {
            checkbox.Disabled = !enabled;
        }
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
}
#endif
