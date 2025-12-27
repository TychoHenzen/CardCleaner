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

    // Auto-tile foldout controls
    private FoldoutContainer? _autoTileFoldout;
    private GridContainer? _variantGrid;
    private readonly TextureRect?[] _variantThumbnails = new TextureRect?[16];
    private AcceptDialog? _variantPickerDialog;
    private TilesetAtlasPicker? _variantPicker;
    private int _editingVariantIndex = -1;

    // Blob settings foldout controls
    private FoldoutContainer? _blobSettingsFoldout;
    private CheckBox? _blobOverrideCheckbox;
    private CheckBox? _blobEnabledField;
    private SpinBox? _blobNoiseScaleField;
    private SpinBox? _blobClusterStrengthField;
    private SpinBox? _blobMinSizeField;
    private SpinBox? _blobMaxSizeField;

    // Decoration density control
    private HBoxContainer? _decorationDensityRow;
    private SpinBox? _decorationDensityField;

    private static readonly string[] BitmaskLabels =
    {
        "None", "N", "E", "N+E", "S", "N+S", "E+S", "N+E+S",
        "W", "N+W", "E+W", "N+E+W", "S+W", "N+S+W", "E+S+W", "All"
    };

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

        // Auto-Tiling foldout
        _autoTileFoldout = new FoldoutContainer("Auto-Tiling", false);
        vbox.AddChild(_autoTileFoldout);

        var autoTileInfo = new Label
        {
            Text = "Assign atlas variants for each neighbor pattern (NESW bitmask):",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        autoTileInfo.AddThemeFontSizeOverride("font_size", 11);
        _autoTileFoldout.Content.AddChild(autoTileInfo);

        _variantGrid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _variantGrid.AddThemeConstantOverride("h_separation", 4);
        _variantGrid.AddThemeConstantOverride("v_separation", 4);
        _autoTileFoldout.Content.AddChild(_variantGrid);

        for (int i = 0; i < 16; i++)
        {
            var slotContainer = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(75, 90),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };

            var label = new Label
            {
                Text = $"{i}: {BitmaskLabels[i]}",
                HorizontalAlignment = HorizontalAlignment.Center
            };
            label.AddThemeFontSizeOverride("font_size", 10);
            slotContainer.AddChild(label);

            var thumbnailPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(48, 48),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            var thumbnail = new TextureRect
            {
                CustomMinimumSize = new Vector2(48, 48),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered
            };
            thumbnailPanel.AddChild(thumbnail);
            slotContainer.AddChild(thumbnailPanel);
            _variantThumbnails[i] = thumbnail;

            var buttonRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            var selectBtn = new Button
            {
                Text = "Set",
                CustomMinimumSize = new Vector2(32, 0)
            };
            int index = i;
            selectBtn.Pressed += () => OpenVariantPickerDialog(index);
            buttonRow.AddChild(selectBtn);

            var clearBtn = new Button
            {
                Text = "X",
                CustomMinimumSize = new Vector2(24, 0),
                TooltipText = "Clear variant"
            };
            clearBtn.Pressed += () => ClearVariant(index);
            buttonRow.AddChild(clearBtn);
            slotContainer.AddChild(buttonRow);

            _variantGrid.AddChild(slotContainer);
        }

        var clearAllBtn = new Button
        {
            Text = "Clear All Variants",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        clearAllBtn.Pressed += ClearAllVariants;
        _autoTileFoldout.Content.AddChild(clearAllBtn);

        vbox.AddChild(new HSeparator());

        // Decoration Density (only for decoration layer tiles)
        _decorationDensityRow = CreateRow("Decoration Density:");
        _decorationDensityField = new SpinBox
        {
            MinValue = 0,
            MaxValue = 100,
            Step = 5,
            Suffix = "%",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _decorationDensityField.ValueChanged += _ => OnFieldChanged("");
        _decorationDensityRow.AddChild(_decorationDensityField);
        _decorationDensityRow.Visible = false; // Hidden by default, shown for decoration layer
        vbox.AddChild(_decorationDensityRow);

        var densityNote = new Label
        {
            Text = "(Probability of decoration appearing in blob regions)",
            Modulate = new Color(0.7f, 0.7f, 0.7f),
            Visible = false
        };
        densityNote.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(densityNote);

        vbox.AddChild(new HSeparator());

        // Blob Settings foldout (per-tile override)
        _blobSettingsFoldout = new FoldoutContainer("Blob Settings Override", false);
        vbox.AddChild(_blobSettingsFoldout);

        _blobOverrideCheckbox = new CheckBox
        {
            Text = "Override global blob settings for this tile"
        };
        _blobOverrideCheckbox.Toggled += OnBlobOverrideToggled;
        _blobSettingsFoldout.Content.AddChild(_blobOverrideCheckbox);

        var blobNote = new Label
        {
            Text = "(When disabled, uses global blob settings from Blob Settings tab)",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        blobNote.AddThemeFontSizeOverride("font_size", 10);
        _blobSettingsFoldout.Content.AddChild(blobNote);

        var blobEnabledRow = CreateRow("Enabled:");
        _blobEnabledField = new CheckBox { ButtonPressed = true };
        _blobEnabledField.Toggled += _ => OnBlobSettingsChanged();
        blobEnabledRow.AddChild(_blobEnabledField);
        _blobSettingsFoldout.Content.AddChild(blobEnabledRow);

        var noiseRow = CreateRow("Noise Scale:");
        _blobNoiseScaleField = new SpinBox
        {
            MinValue = 0.01,
            MaxValue = 1.0,
            Step = 0.01,
            Value = 0.15,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _blobNoiseScaleField.ValueChanged += _ => OnBlobSettingsChanged();
        noiseRow.AddChild(_blobNoiseScaleField);
        _blobSettingsFoldout.Content.AddChild(noiseRow);

        var clusterRow = CreateRow("Cluster Strength:");
        _blobClusterStrengthField = new SpinBox
        {
            MinValue = 0.0,
            MaxValue = 1.0,
            Step = 0.05,
            Value = 0.7,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _blobClusterStrengthField.ValueChanged += _ => OnBlobSettingsChanged();
        clusterRow.AddChild(_blobClusterStrengthField);
        _blobSettingsFoldout.Content.AddChild(clusterRow);

        var minSizeRow = CreateRow("Min Blob Size:");
        _blobMinSizeField = new SpinBox
        {
            MinValue = 1,
            MaxValue = 50,
            Step = 1,
            Value = 3,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _blobMinSizeField.ValueChanged += _ => OnBlobSettingsChanged();
        minSizeRow.AddChild(_blobMinSizeField);
        _blobSettingsFoldout.Content.AddChild(minSizeRow);

        var maxSizeRow = CreateRow("Max Blob Size:");
        _blobMaxSizeField = new SpinBox
        {
            MinValue = 1,
            MaxValue = 100,
            Step = 1,
            Value = 12,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _blobMaxSizeField.ValueChanged += _ => OnBlobSettingsChanged();
        maxSizeRow.AddChild(_blobMaxSizeField);
        _blobSettingsFoldout.Content.AddChild(maxSizeRow);

        // Initially disable blob fields
        SetBlobFieldsEnabled(false);

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

        // Update auto-tile variant thumbnails
        for (int i = 0; i < 16; i++)
        {
            UpdateVariantThumbnail(i);
        }

        // Show decoration density only for decoration layer tiles
        var isDecorationLayer = _currentTile.Layer.ToLowerInvariant() == "decoration";
        _decorationDensityRow!.Visible = isDecorationLayer;
        _decorationDensityField!.Value = _currentTile.DecorationDensity * 100;

        // Populate blob settings
        var hasBlobOverride = _currentTile.BlobSettings != null;
        _blobOverrideCheckbox!.ButtonPressed = hasBlobOverride;
        SetBlobFieldsEnabled(hasBlobOverride);

        if (hasBlobOverride)
        {
            _blobEnabledField!.ButtonPressed = _currentTile.BlobSettings!.Enabled;
            _blobNoiseScaleField!.Value = _currentTile.BlobSettings.NoiseScale;
            _blobClusterStrengthField!.Value = _currentTile.BlobSettings.ClusterStrength;
            _blobMinSizeField!.Value = _currentTile.BlobSettings.MinBlobSize;
            _blobMaxSizeField!.Value = _currentTile.BlobSettings.MaxBlobSize;
        }
        else
        {
            // Show global defaults as placeholder values
            var globalConfig = _service.BlobConfig;
            _blobEnabledField!.ButtonPressed = globalConfig.Enabled;
            _blobNoiseScaleField!.Value = globalConfig.NoiseScale;
            _blobClusterStrengthField!.Value = globalConfig.ClusterStrength;
            _blobMinSizeField!.Value = globalConfig.MinBlobSize;
            _blobMaxSizeField!.Value = globalConfig.MaxBlobSize;
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

        // Update decoration density visibility when layer changes
        var isDecorationLayer = _currentTile.Layer.ToLowerInvariant() == "decoration";
        _decorationDensityRow!.Visible = isDecorationLayer;

        // Update decoration density value
        _currentTile.DecorationDensity = (float)(_decorationDensityField!.Value / 100.0);

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

    private void OpenVariantPickerDialog(int variantIndex)
    {
        if (_currentTile == null) return;

        _editingVariantIndex = variantIndex;

        // Initialize AutoTileVariants if needed
        _currentTile.AutoTileVariants ??= new Vector2I?[16];

        // Create dialog lazily
        if (_variantPickerDialog == null)
        {
            _variantPickerDialog = new AcceptDialog
            {
                Title = "Select Atlas Variant",
                InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
                Size = new Vector2I(550, 450),
                OkButtonText = "Assign",
            };

            var dialogVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };

            var pickerScroll = new ScrollContainer
            {
                CustomMinimumSize = new Vector2(0, 380),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Auto
            };

            _variantPicker = new TilesetAtlasPicker();
            pickerScroll.AddChild(_variantPicker);
            dialogVBox.AddChild(pickerScroll);

            _variantPickerDialog.AddChild(dialogVBox);
            AddChild(_variantPickerDialog);

            // Handle dialog confirmed
            _variantPickerDialog.Confirmed += OnVariantPickerConfirmed;
            _variantPickerDialog.Canceled += OnVariantPickerCanceled;
        }

        // Configure picker with current tile's atlas source
        var source = _service.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _variantPicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _variantPicker.SetSource(source, tileSize, _currentTile.SourceId);

            // Pre-select current variant or base tile coords
            if (_currentTile.AutoTileVariants[variantIndex].HasValue)
            {
                _variantPicker.SelectedCoords = _currentTile.AutoTileVariants[variantIndex]!.Value;
            }
            else
            {
                _variantPicker.SelectedCoords = new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
            }
        }

        _variantPickerDialog.Title = $"Select Variant for Bitmask {variantIndex}: {BitmaskLabels[variantIndex]}";
        _variantPickerDialog.Popup();
    }

    private void OnVariantPickerConfirmed()
    {
        if (_editingVariantIndex < 0 || _currentTile == null || _variantPicker == null) return;

        _currentTile.AutoTileVariants ??= new Vector2I?[16];
        _currentTile.AutoTileVariants[_editingVariantIndex] = _variantPicker.SelectedCoords;
        UpdateVariantThumbnail(_editingVariantIndex);
        _service.UpdateTile(_currentTile);

        _editingVariantIndex = -1;
    }

    private void OnVariantPickerCanceled()
    {
        _editingVariantIndex = -1;
    }

    private void ClearVariant(int index)
    {
        if (_currentTile?.AutoTileVariants == null) return;

        _currentTile.AutoTileVariants[index] = null;
        UpdateVariantThumbnail(index);
        _service.UpdateTile(_currentTile);
    }

    private void ClearAllVariants()
    {
        if (_currentTile == null) return;

        _currentTile.AutoTileVariants = null;
        for (int i = 0; i < 16; i++)
        {
            UpdateVariantThumbnail(i);
        }
        _service.UpdateTile(_currentTile);
    }

    private void UpdateVariantThumbnail(int index)
    {
        if (_currentTile == null || _variantThumbnails[index] == null) return;

        var thumbnail = _variantThumbnails[index]!;

        // Check if variant is defined
        if (_currentTile.AutoTileVariants == null || !_currentTile.AutoTileVariants[index].HasValue)
        {
            thumbnail.Texture = null;
            return;
        }

        var coords = _currentTile.AutoTileVariants[index]!.Value;
        var texture = _service.GetTileTexture(_currentTile);
        if (texture == null)
        {
            thumbnail.Texture = null;
            return;
        }

        var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        var region = new Rect2I(coords * tileSize, tileSize);

        var atlasTex = new AtlasTexture
        {
            Atlas = texture,
            Region = region
        };
        thumbnail.Texture = atlasTex;
    }

    private void OnBlobOverrideToggled(bool enabled)
    {
        if (_isUpdating || _currentTile == null) return;

        SetBlobFieldsEnabled(enabled);

        if (enabled)
        {
            // Create blob settings from current field values (which show global defaults)
            _currentTile.BlobSettings = new EditableBlobConfig
            {
                Enabled = _blobEnabledField!.ButtonPressed,
                NoiseScale = (float)_blobNoiseScaleField!.Value,
                ClusterStrength = (float)_blobClusterStrengthField!.Value,
                MinBlobSize = (int)_blobMinSizeField!.Value,
                MaxBlobSize = (int)_blobMaxSizeField!.Value
            };
        }
        else
        {
            // Clear per-tile settings, use global
            _currentTile.BlobSettings = null;
        }

        _service.UpdateTile(_currentTile);
    }

    private void OnBlobSettingsChanged()
    {
        if (_isUpdating || _currentTile == null) return;
        if (!_blobOverrideCheckbox!.ButtonPressed) return; // Only update if override is enabled

        // Ensure min <= max
        if (_blobMinSizeField!.Value > _blobMaxSizeField!.Value)
        {
            _isUpdating = true;
            _blobMaxSizeField.Value = _blobMinSizeField.Value;
            _isUpdating = false;
        }

        _currentTile.BlobSettings = new EditableBlobConfig
        {
            Enabled = _blobEnabledField!.ButtonPressed,
            NoiseScale = (float)_blobNoiseScaleField!.Value,
            ClusterStrength = (float)_blobClusterStrengthField!.Value,
            MinBlobSize = (int)_blobMinSizeField.Value,
            MaxBlobSize = (int)_blobMaxSizeField.Value
        };

        _service.UpdateTile(_currentTile);
    }

    private void SetBlobFieldsEnabled(bool enabled)
    {
        _blobEnabledField!.Disabled = !enabled;
        _blobNoiseScaleField!.Editable = enabled;
        _blobClusterStrengthField!.Editable = enabled;
        _blobMinSizeField!.Editable = enabled;
        _blobMaxSizeField!.Editable = enabled;

        // Visual feedback: dim fields when disabled
        var color = enabled ? Colors.White : new Color(0.6f, 0.6f, 0.6f);
        _blobEnabledField.Modulate = color;
        _blobNoiseScaleField.Modulate = color;
        _blobClusterStrengthField.Modulate = color;
        _blobMinSizeField.Modulate = color;
        _blobMaxSizeField.Modulate = color;
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
