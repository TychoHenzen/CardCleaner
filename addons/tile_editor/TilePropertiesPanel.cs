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
    private readonly TileEditorService? _service;
    private string? _selectedTileId;
    private EditableTile? _currentTile;

    // Atlas picker controls
    private OptionButton? _sourceDropdown;
    private Button? _sourcePickerButton;
    private Button? _atlasCoordButton;

    // Source picker dialog
    private AcceptDialog? _sourcePickerDialog;
    private int _selectedSourceIdForPicker;
    private Dictionary<int, PanelContainer>? _sourcePanelsBySourceId;
    private Dictionary<int, string>? _sourceDisplayNames;
    private List<TextureRect>? _sourcePickerThumbnails;
    private HSlider? _sourcePickerZoomSlider;
    private LineEdit? _sourcePickerFilterField;
    private float _sourcePickerZoom = 1.0f;
    private TextureRect? _atlasButtonThumbnail;
    private Label? _atlasButtonLabel;
    private AcceptDialog? _atlasPickerDialog;
    private TilesetAtlasPicker? _atlasDialogPicker;

    // Form fields
    private LineEdit? _idField;
    private LineEdit? _nameField;
    private OptionButton? _tileModeDropdown;
    private OptionButton? _passabilityField;
    private SpinBox? _sourceIdField;
    private OptionButton? _layerField;
    private SpinBox? _elevationField;
    private CheckBox? _transparentField;
    private SpinBox? _sizeXField;
    private SpinBox? _sizeYField;
    private OptionButton? _sourceScaleDropdown;
    private VBoxContainer? _biomesContainer;
    private readonly Dictionary<string, CheckBox> _biomeCheckboxes = new();
    private TextEdit? _descriptionField;
    private Label? _validationLabel;

    // General properties foldout controls
    private FoldoutContainer? _generalFoldout;

    // Auto-tile foldout controls
    private FoldoutContainer? _autoTileFoldout;
    private OptionButton? _autoTileFormatDropdown;
    private Label? _formatDescriptionLabel;
    private OptionButton? _innerTerrainDropdown;
    private OptionButton? _outerTerrainDropdown;
    private VBoxContainer? _variantGridContainer;
    private GridContainer? _variantGrid;
    private TextureRect?[]? _variantThumbnails;
    private TileShapePreview?[]? _variantShapePreviews;
    private AcceptDialog? _variantPickerDialog;
    private TilesetAtlasPicker? _variantPicker;
    private int _editingVariantIndex = -1;
    private int _currentVariantCount = 16;

    // Advanced variant editor (for custom variant definitions with size/offset)
    private FoldoutContainer? _advancedVariantsFoldout;
    private VariantMappingEditor? _variantMappingEditor;
    private CheckBox? _useAdvancedVariantsCheckbox;

    // Decoration density control
    private HBoxContainer? _decorationDensityRow;
    private SpinBox? _decorationDensityField;

    // Variations foldout controls
    private FoldoutContainer? _variationsFoldout;
    private OptionButton? _variationModeDropdown;
    private VBoxContainer? _variationsListContainer;
    private AcceptDialog? _variationPickerDialog;
    private TilesetAtlasPicker? _variationPicker;

    // Animation foldout controls
    private FoldoutContainer? _animationFoldout;
    private SpinBox? _animationFrameDurationField;
    private VBoxContainer? _animationFramesContainer;
    private AcceptDialog? _animationFramePickerDialog;
    private TilesetAtlasPicker? _animationFramePicker;

    // 4-bit corner format labels (NE=1, SE=2, SW=4, NW=8)
    private static readonly string[] CornerBitmaskLabels =
    {
        "None", "NE", "SE", "NE+SE", "SW", "NE+SW", "SE+SW", "NE+SE+SW",
        "NW", "NE+NW", "SE+NW", "NE+SE+NW", "SW+NW", "NE+SW+NW", "SE+SW+NW", "All"
    };

    // 4-bit edge format labels (N=1, E=2, S=4, W=8)
    private static readonly string[] EdgeBitmaskLabels =
    {
        "None", "N", "E", "N+E", "S", "N+S", "E+S", "N+E+S",
        "W", "N+W", "E+W", "N+E+W", "S+W", "N+S+W", "E+S+W", "All"
    };

    // 8-bit blob format - we only show the 47 valid combinations
    // Generated from NeighborBitmask8.GetValid47Masks()
    private static string GetBlobMaskLabel(int index, int mask)
    {
        if (mask == 0) return "None";
        if (mask == 255) return "All";

        var parts = new System.Collections.Generic.List<string>();
        if ((mask & 1) != 0) parts.Add("N");
        if ((mask & 2) != 0) parts.Add("NE");
        if ((mask & 4) != 0) parts.Add("E");
        if ((mask & 8) != 0) parts.Add("SE");
        if ((mask & 16) != 0) parts.Add("S");
        if ((mask & 32) != 0) parts.Add("SW");
        if ((mask & 64) != 0) parts.Add("W");
        if ((mask & 128) != 0) parts.Add("NW");
        return string.Join("+", parts);
    }

    private bool _isUpdating;

    // Required by Godot for [Tool] classes
    public TilePropertiesPanel() { }

    public TilePropertiesPanel(TileEditorService service)
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

        // Description field
        vbox.AddChild(new Label { Text = "Description:" });
        _descriptionField = new TextEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 80),
            PlaceholderText = "Visual appearance: color, texture, features...",
            WrapMode = TextEdit.LineWrappingMode.Boundary
        };
        _descriptionField.TextChanged += OnDescriptionChanged;
        vbox.AddChild(_descriptionField);

        var descNote = new Label
        {
            Text = "(For artists and AI image generators)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        descNote.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(descNote);

        // Tile Mode selector
        var modeRow = CreateRow("Tile Mode:");
        _tileModeDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tileModeDropdown.AddItem("Plain Tile", 0);
        _tileModeDropdown.AddItem("Per-Tile Variations", 1);
        _tileModeDropdown.AddItem("Per-Map Variations", 2);
        _tileModeDropdown.AddItem("Auto-Tile", 3);
        _tileModeDropdown.AddItem("Per-Map Variation Auto-Tile", 4);
        _tileModeDropdown.AddItem("Animated Tile", 5);
        _tileModeDropdown.ItemSelected += OnTileModeChanged;
        modeRow.AddChild(_tileModeDropdown);
        vbox.AddChild(modeRow);

        vbox.AddChild(new HSeparator());

        // General Properties foldout
        _generalFoldout = new FoldoutContainer("General Properties", false);
        vbox.AddChild(_generalFoldout);

        // Passability dropdown
        var passRow = CreateRow("Passability:");
        _passabilityField = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _passabilityField.AddItem("Passable", 0);
        _passabilityField.AddItem("Solid", 1);
        _passabilityField.AddItem("Partially Passable", 2);
        _passabilityField.ItemSelected += _ => OnFieldChanged("");
        passRow.AddChild(_passabilityField);
        _generalFoldout.Content.AddChild(passRow);

        // Layer dropdown
        var layerRow = CreateRow("Layer:");
        _layerField = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _layerField.AddItem("Terrain", 0);
        _layerField.AddItem("Decoration", 1);
        _layerField.AddItem("Structure", 2);
        _layerField.AddItem("Effects", 3);
        _layerField.ItemSelected += _ => OnFieldChanged("");
        layerRow.AddChild(_layerField);
        _generalFoldout.Content.AddChild(layerRow);

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
        _generalFoldout.Content.AddChild(elevRow);

        // Transparent
        var transRow = CreateRow("Transparent:");
        _transparentField = new CheckBox();
        _transparentField.Toggled += _ => OnFieldChanged("");
        transRow.AddChild(_transparentField);
        _generalFoldout.Content.AddChild(transRow);

        // Size section (for multi-cell tiles like trees)
        var sizeHeader = new Label { Text = "Tile Size (cells)" };
        sizeHeader.AddThemeFontSizeOverride("font_size", 12);
        _generalFoldout.Content.AddChild(sizeHeader);

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
        _generalFoldout.Content.AddChild(sizeRow);

        var sizeNote = new Label
        {
            Text = "(For multi-cell tiles like trees: 2x2, etc.)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        sizeNote.AddThemeFontSizeOverride("font_size", 11);
        _generalFoldout.Content.AddChild(sizeNote);

        // Source Scale dropdown (for tiles from differently-sized source textures)
        var scaleRow = CreateRow("Source Scale:");
        _sourceScaleDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sourceScaleDropdown.AddItem("0.5x (32px source)", 0);
        _sourceScaleDropdown.AddItem("1.0x (16px source)", 1);
        _sourceScaleDropdown.AddItem("2.0x (8px source)", 2);
        _sourceScaleDropdown.Selected = 1; // Default to 1.0x
        _sourceScaleDropdown.ItemSelected += _ => OnFieldChanged("");
        scaleRow.AddChild(_sourceScaleDropdown);
        _generalFoldout.Content.AddChild(scaleRow);

        var scaleNote = new Label
        {
            Text = "(For tiles from 8x8 or 32x32 source textures)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        scaleNote.AddThemeFontSizeOverride("font_size", 11);
        _generalFoldout.Content.AddChild(scaleNote);

        // Biomes section
        _generalFoldout.Content.AddChild(new Label { Text = "Allowed Biomes:" });
        _biomesContainer = new VBoxContainer();
        _generalFoldout.Content.AddChild(_biomesContainer);

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
        _generalFoldout.Content.AddChild(biomeNote);

        vbox.AddChild(new HSeparator());

        // Atlas selection section
        var atlasHeader = new Label { Text = "Atlas Coordinates" };
        atlasHeader.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(atlasHeader);

        // Source selection button (opens visual picker)
        var sourceRow = CreateRow("Source:");
        _sourceDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false // Hidden, used for internal tracking only
        };
        _sourceDropdown.ItemSelected += OnSourceDropdownChanged;
        sourceRow.AddChild(_sourceDropdown);

        _sourcePickerButton = new Button
        {
            Text = "Click to select source...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sourcePickerButton.Pressed += OpenSourcePickerDialog;
        sourceRow.AddChild(_sourcePickerButton);
        vbox.AddChild(sourceRow);

        // Atlas coordinate button with thumbnail preview
        var coordRow = CreateRow("Coords:");
        _atlasCoordButton = new Button { SizeFlagsHorizontal = SizeFlags.ExpandFill };

        var buttonContent = new HBoxContainer();
        _atlasButtonThumbnail = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };
        buttonContent.AddChild(_atlasButtonThumbnail);

        _atlasButtonLabel = new Label
        {
            Text = "Click to select...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        buttonContent.AddChild(_atlasButtonLabel);

        _atlasCoordButton.AddChild(buttonContent);
        _atlasCoordButton.Pressed += OpenAtlasPickerDialog;
        coordRow.AddChild(_atlasCoordButton);
        vbox.AddChild(coordRow);

        // Hidden source ID field for internal tracking
        // MaxValue must be high enough to accommodate all source IDs (can be 500+)
        _sourceIdField = new SpinBox { Visible = false, Value = 4, MinValue = 0, MaxValue = 10000 };
        AddChild(_sourceIdField);

        vbox.AddChild(new HSeparator());

        // Auto-Tiling foldout
        _autoTileFoldout = new FoldoutContainer("Auto-Tiling", false);
        vbox.AddChild(_autoTileFoldout);

        // Format selector - populated from registry
        var formatRow = CreateRow("Format:");
        _autoTileFormatDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        PopulateFormatDropdown();
        _autoTileFormatDropdown.ItemSelected += OnAutoTileFormatChanged;
        formatRow.AddChild(_autoTileFormatDropdown);
        _autoTileFoldout.Content.AddChild(formatRow);

        // Format description label
        _formatDescriptionLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        _formatDescriptionLabel.AddThemeFontSizeOverride("font_size", 10);
        _autoTileFoldout.Content.AddChild(_formatDescriptionLabel);

        _autoTileFoldout.Content.AddChild(new HSeparator());

        // Terrain Transition section header
        var transitionHeader = new Label { Text = "Terrain Transitions" };
        transitionHeader.AddThemeFontSizeOverride("font_size", 12);
        _autoTileFoldout.Content.AddChild(transitionHeader);

        var transitionInfo = new Label
        {
            Text = "Configure which terrains this auto-tile transitions between:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        transitionInfo.AddThemeFontSizeOverride("font_size", 10);
        _autoTileFoldout.Content.AddChild(transitionInfo);

        // Inner Terrain (the border terrain shown)
        var innerRow = CreateRow("Inner Terrain:");
        _innerTerrainDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "The terrain whose border is rendered. Defaults to this tile if not set."
        };
        _innerTerrainDropdown.ItemSelected += OnInnerTerrainChanged;
        innerRow.AddChild(_innerTerrainDropdown);
        _autoTileFoldout.Content.AddChild(innerRow);

        // Outer Terrain (the background terrain)
        var outerRow = CreateRow("Outer Terrain:");
        _outerTerrainDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Background terrain. Use '*' for compositable (transparent, works with any base)."
        };
        _outerTerrainDropdown.ItemSelected += OnOuterTerrainChanged;
        outerRow.AddChild(_outerTerrainDropdown);
        _autoTileFoldout.Content.AddChild(outerRow);

        var outerNote = new Label
        {
            Text = "'*' = Compositable (transparent border, composited onto base terrains at compile time)\n" +
                   "Specific terrain = Fixed transition (pre-baked, only works with that terrain)",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.6f, 0.6f, 0.6f)
        };
        outerNote.AddThemeFontSizeOverride("font_size", 9);
        _autoTileFoldout.Content.AddChild(outerNote);

        _autoTileFoldout.Content.AddChild(new HSeparator());

        var autoTileInfo = new Label
        {
            Text = "Assign atlas variants for each neighbor pattern:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        autoTileInfo.AddThemeFontSizeOverride("font_size", 11);
        _autoTileFoldout.Content.AddChild(autoTileInfo);

        // Container for the variant grid (will be rebuilt when format changes)
        _variantGridContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _autoTileFoldout.Content.AddChild(_variantGridContainer);

        // Build initial grid for 16 variants (corner16 is default)
        RebuildVariantGrid(16, "corner16");

        var clearAllBtn = new Button
        {
            Text = "Clear All Variants",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        clearAllBtn.Pressed += ClearAllVariants;
        _autoTileFoldout.Content.AddChild(clearAllBtn);

        _autoTileFoldout.Content.AddChild(new HSeparator());

        // Advanced Variants section (for multi-cell variants with size/offset)
        _advancedVariantsFoldout = new FoldoutContainer("Advanced Variant Configuration", true); // Collapsed by default
        _autoTileFoldout.Content.AddChild(_advancedVariantsFoldout);

        var advancedInfo = new Label
        {
            Text = "Configure multi-cell variants with custom sizes and offsets (e.g., 3-tile-tall walls):",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        advancedInfo.AddThemeFontSizeOverride("font_size", 10);
        _advancedVariantsFoldout.Content.AddChild(advancedInfo);

        // Checkbox to enable advanced variants
        var advancedRow = new HBoxContainer();
        _useAdvancedVariantsCheckbox = new CheckBox
        {
            Text = "Enable advanced variant definitions",
            TooltipText = "When enabled, allows configuring size and offset for each variant"
        };
        _useAdvancedVariantsCheckbox.Toggled += OnAdvancedVariantsToggled;
        advancedRow.AddChild(_useAdvancedVariantsCheckbox);
        _advancedVariantsFoldout.Content.AddChild(advancedRow);

        // Variant mapping editor (hidden until checkbox is checked)
        _variantMappingEditor = new VariantMappingEditor(_service!);
        _variantMappingEditor.Visible = false;
        _variantMappingEditor.VariantsModified += OnVariantMappingsModified;
        _advancedVariantsFoldout.Content.AddChild(_variantMappingEditor);

        vbox.AddChild(new HSeparator());

        // Variations foldout (visual variations - multiple atlas coords for same tile type)
        _variationsFoldout = new FoldoutContainer("Tile Variations", false);
        vbox.AddChild(_variationsFoldout);

        var variationsInfo = new Label
        {
            Text = "Add visual variations for this tile type:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        variationsInfo.AddThemeFontSizeOverride("font_size", 11);
        _variationsFoldout.Content.AddChild(variationsInfo);

        // Variation mode dropdown
        var variationModeRow = CreateRow("Mode:");
        _variationModeDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _variationModeDropdown.AddItem("Per Instance (random each tile)", 0);
        _variationModeDropdown.AddItem("Per Generation (one for whole map)", 1);
        _variationModeDropdown.ItemSelected += OnVariationModeChanged;
        variationModeRow.AddChild(_variationModeDropdown);
        _variationsFoldout.Content.AddChild(variationModeRow);

        var modeNote = new Label
        {
            Text = "Per Instance: Each tile placement uses random variant.\nPer Generation: One variant chosen at map start.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        modeNote.AddThemeFontSizeOverride("font_size", 9);
        _variationsFoldout.Content.AddChild(modeNote);

        // Container for variation entries
        _variationsListContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _variationsFoldout.Content.AddChild(_variationsListContainer);

        // Add variation button
        var addVariationBtn = new Button
        {
            Text = "+ Add Variation",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        addVariationBtn.Pressed += OpenAddVariationDialog;
        _variationsFoldout.Content.AddChild(addVariationBtn);

        vbox.AddChild(new HSeparator());

        // Animation foldout
        _animationFoldout = new FoldoutContainer("Tile Animation", false);
        vbox.AddChild(_animationFoldout);

        var animationInfo = new Label
        {
            Text = "Configure animation frames for this tile:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        animationInfo.AddThemeFontSizeOverride("font_size", 11);
        _animationFoldout.Content.AddChild(animationInfo);

        // Frame duration
        var durationRow = CreateRow("Frame Duration:");
        _animationFrameDurationField = new SpinBox
        {
            MinValue = 0.05,
            MaxValue = 5.0,
            Step = 0.05,
            Value = 0.2,
            Suffix = "s",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _animationFrameDurationField.ValueChanged += OnAnimationDurationChanged;
        durationRow.AddChild(_animationFrameDurationField);
        _animationFoldout.Content.AddChild(durationRow);

        // Container for animation frame entries
        _animationFramesContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _animationFoldout.Content.AddChild(_animationFramesContainer);

        // Add frame button
        var addFrameBtn = new Button
        {
            Text = "+ Add Frame",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        addFrameBtn.Pressed += OpenAddAnimationFrameDialog;
        _animationFoldout.Content.AddChild(addFrameBtn);

        var animNote = new Label
        {
            Text = "Note: Animation plays automatically in Godot's TileMap if configured in the TileSet.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        animNote.AddThemeFontSizeOverride("font_size", 9);
        _animationFoldout.Content.AddChild(animNote);

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

    private void PopulateFormatDropdown()
    {
        if (_autoTileFormatDropdown == null) return;

        _autoTileFormatDropdown.Clear();

        var formatNames = _service.GetAvailableFormatNames();
        var index = 0;
        foreach (var formatName in formatNames)
        {
            var displayName = _service.GetFormatDisplayName(formatName);
            var format = _service.GetFormatDefinition(formatName);
            var variantCount = format?.AllowedBitmasks.Count ?? 16;

            // Show format name with variant count
            _autoTileFormatDropdown.AddItem($"{displayName} ({variantCount} variants)", index);
            _autoTileFormatDropdown.SetItemMetadata(index, formatName);
            index++;
        }
    }

    private void UpdateFormatDescription(string formatName)
    {
        if (_formatDescriptionLabel == null) return;

        var format = _service.GetFormatDefinition(formatName);
        if (format == null)
        {
            _formatDescriptionLabel.Text = "";
            return;
        }

        var typeDesc = format.BitmaskType switch
        {
            CardCleaner.Features.Worldgen.AutoTiling.BitmaskType.Corner4 => "4-bit corner bitmask (NE, SE, SW, NW)",
            CardCleaner.Features.Worldgen.AutoTiling.BitmaskType.Edge4 => "4-bit edge bitmask (N, E, S, W)",
            CardCleaner.Features.Worldgen.AutoTiling.BitmaskType.Full8 => "8-bit full bitmask (8 neighbors)",
            _ => "Unknown bitmask type"
        };

        _formatDescriptionLabel.Text = format.IsBuiltIn
            ? $"Built-in format: {typeDesc}"
            : $"Custom format: {typeDesc}";
    }

    private int GetFormatDropdownIndex(string formatName)
    {
        if (_autoTileFormatDropdown == null) return 0;

        for (int i = 0; i < _autoTileFormatDropdown.ItemCount; i++)
        {
            var metadata = _autoTileFormatDropdown.GetItemMetadata(i).AsString();
            if (string.Equals(metadata, formatName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }
        return 0; // Default to first format
    }

    private void OnSourceDropdownChanged(long index)
    {
        if (_isUpdating || _sourceDropdown == null || _currentTile == null) return;

        var sourceId = _sourceDropdown.GetItemId((int)index);
        _sourceIdField!.Value = sourceId;
        _currentTile.SourceId = sourceId;

        // Update button thumbnail with new source
        UpdateSourceButtonText();
        UpdateAtlasButtonAppearance();
        OnFieldChanged("");
    }

    private void UpdateSourceButtonText()
    {
        if (_sourcePickerButton == null || _currentTile == null) return;

        var sources = _service.GetAvailableAtlasSources();
        var sourceInfo = sources.Find(s => s.SourceId == _currentTile.SourceId);
        _sourcePickerButton.Text = sourceInfo?.DisplayName ?? $"Source {_currentTile.SourceId}";
    }

    public void SelectTile(string tileId)
    {
        if (_service == null) return;

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
        _descriptionField!.Text = _currentTile.Description ?? "";

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

        // Update atlas button appearance
        _sourceIdField!.Value = _currentTile.SourceId;
        UpdateSourceButtonText();
        UpdateAtlasButtonAppearance();

        // Set tile mode dropdown
        _tileModeDropdown!.Selected = _currentTile.TileMode?.ToLowerInvariant() switch
        {
            "pertilevariations" => 1,
            "permapvariations" => 2,
            "autotile" => 3,
            "permapvariationautotile" => 4,
            "animated" => 5,
            _ => 0
        };

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

        // Source scale dropdown (0.5x=0, 1.0x=1, 2.0x=2)
        _sourceScaleDropdown!.Selected = _currentTile.SourceScale switch
        {
            0.5f => 0,
            2.0f => 2,
            _ => 1 // Default to 1.0x
        };

        // Update biome checkboxes
        foreach (var (biome, checkbox) in _biomeCheckboxes)
        {
            checkbox.ButtonPressed = _currentTile.Biomes.Contains(biome, StringComparer.OrdinalIgnoreCase);
        }

        // Update auto-tile format dropdown and rebuild grid
        var currentFormat = _currentTile.AutoTileFormat ?? "corner16";
        _autoTileFormatDropdown!.Selected = GetFormatDropdownIndex(currentFormat);
        UpdateFormatDescription(currentFormat);
        var format = _service.GetFormatDefinition(currentFormat);
        var variantCount = format?.AllowedBitmasks.Count ?? 16;
        RebuildVariantGrid(variantCount, currentFormat);

        // Populate and select terrain transition dropdowns
        PopulateTerrainDropdowns();

        // Set inner terrain selection
        if (string.IsNullOrEmpty(_currentTile.InnerTerrainId))
        {
            _innerTerrainDropdown!.Selected = 0; // Default
        }
        else
        {
            var terrainTiles = _service.AllTiles
                .Where(t => t.Layer == "terrain" && !t.HasAutoTileVariants)
                .OrderBy(t => t.Name)
                .ToList();
            var innerIndex = terrainTiles.FindIndex(t => t.Id == _currentTile.InnerTerrainId);
            _innerTerrainDropdown!.Selected = innerIndex >= 0 ? innerIndex + 1 : 0;
        }

        // Set outer terrain selection
        if (string.IsNullOrEmpty(_currentTile.OuterTerrainId))
        {
            _outerTerrainDropdown!.Selected = 0; // None
        }
        else if (_currentTile.OuterTerrainId == "*")
        {
            _outerTerrainDropdown!.Selected = 1; // Compositable
        }
        else
        {
            var terrainTiles = _service.AllTiles
                .Where(t => t.Layer == "terrain" && !t.HasAutoTileVariants)
                .OrderBy(t => t.Name)
                .ToList();
            var outerIndex = terrainTiles.FindIndex(t => t.Id == _currentTile.OuterTerrainId);
            _outerTerrainDropdown!.Selected = outerIndex >= 0 ? outerIndex + 2 : 0;
        }

        // Show decoration density only for decoration layer tiles
        var isDecorationLayer = _currentTile.Layer.ToLowerInvariant() == "decoration";
        _decorationDensityRow!.Visible = isDecorationLayer;
        _decorationDensityField!.Value = _currentTile.DecorationDensity * 100;

        // Populate variation settings
        var variationModeIndex = _currentTile.VariationMode?.ToLowerInvariant() switch
        {
            "pergeneration" or "per_generation" => 1,
            _ => 0
        };
        _variationModeDropdown!.Selected = variationModeIndex;
        RebuildVariationsList();

        // Populate animation settings
        _animationFrameDurationField!.Value = _currentTile.AnimationFrameDuration;
        RebuildAnimationFramesList();

        // Update advanced variants UI
        UpdateAdvancedVariantsUI();

        _validationLabel!.Text = "";
        _isUpdating = false;

        // Update section visibility based on tile mode
        UpdateSectionVisibility();
    }

    private void OnDescriptionChanged()
    {
        if (_isUpdating || _currentTile == null) return;

        _currentTile.Description = string.IsNullOrWhiteSpace(_descriptionField!.Text)
            ? null
            : _descriptionField.Text;

        _service.UpdateTile(_currentTile);
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

        _currentTile.SourceId = (int)_sourceIdField!.Value;

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

        // Source scale (0=0.5x, 1=1.0x, 2=2.0x)
        _currentTile.SourceScale = _sourceScaleDropdown!.Selected switch
        {
            0 => 0.5f,
            2 => 2.0f,
            _ => 1.0f
        };

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
        _descriptionField!.Editable = enabled;
        _tileModeDropdown!.Disabled = !enabled;
        _passabilityField!.Disabled = !enabled;
        _sourceDropdown!.Disabled = !enabled;
        _sourcePickerButton!.Disabled = !enabled;
        _atlasCoordButton!.Disabled = !enabled;
        _layerField!.Disabled = !enabled;
        _elevationField!.Editable = enabled;
        _transparentField!.Disabled = !enabled;
        _sizeXField!.Editable = enabled;
        _sizeYField!.Editable = enabled;
        _sourceScaleDropdown!.Disabled = !enabled;

        foreach (var checkbox in _biomeCheckboxes.Values)
        {
            checkbox.Disabled = !enabled;
        }
    }

    private void UpdateAtlasButtonAppearance()
    {
        if (_currentTile == null || _atlasButtonLabel == null || _atlasButtonThumbnail == null) return;

        _atlasButtonLabel.Text = $"({_currentTile.AtlasX}, {_currentTile.AtlasY})";

        var texture = _service.GetTileTexture(_currentTile);
        if (texture != null)
        {
            var baseTileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            // Account for source scale: 0.5x = 32px source, 2.0x = 8px source
            var actualTileSize = new Vector2I(
                (int)(baseTileSize.X / _currentTile.SourceScale),
                (int)(baseTileSize.Y / _currentTile.SourceScale)
            );
            var region = new Rect2I(
                new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY) * actualTileSize,
                actualTileSize
            );
            var atlasTex = new AtlasTexture
            {
                Atlas = texture,
                Region = region
            };
            _atlasButtonThumbnail.Texture = atlasTex;
        }
        else
        {
            _atlasButtonThumbnail.Texture = null;
        }
    }

    private void OpenAtlasPickerDialog()
    {
        if (_currentTile == null) return;

        // Create dialog lazily
        if (_atlasPickerDialog == null)
        {
            _atlasPickerDialog = new AcceptDialog
            {
                Title = "Select Atlas Coordinates",
                InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
                Size = GetLargeDialogSize(),
                OkButtonText = "Select"
            };

            var dialogVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };

            var pickerScroll = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Auto
            };

            _atlasDialogPicker = new TilesetAtlasPicker();
            pickerScroll.AddChild(_atlasDialogPicker);
            dialogVBox.AddChild(pickerScroll);

            _atlasPickerDialog.AddChild(dialogVBox);
            AddChild(_atlasPickerDialog);

            _atlasPickerDialog.Confirmed += OnAtlasPickerConfirmed;
        }

        // Configure picker with current tile's atlas source
        var source = _service.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _atlasDialogPicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _atlasDialogPicker.SetSource(source, tileSize, _currentTile.SourceId, _currentTile.SourceScale);
            _atlasDialogPicker.SelectedCoords = new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
            _atlasDialogPicker.SelectedSize = new Vector2I(_currentTile.SizeX, _currentTile.SizeY);
        }

        _atlasPickerDialog.Popup();
    }

    private void OnAtlasPickerConfirmed()
    {
        if (_currentTile == null || _atlasDialogPicker == null) return;

        _currentTile.AtlasX = _atlasDialogPicker.SelectedCoords.X;
        _currentTile.AtlasY = _atlasDialogPicker.SelectedCoords.Y;
        UpdateAtlasButtonAppearance();
        _service.UpdateTile(_currentTile);
    }

    private void OpenSourcePickerDialog()
    {
        if (_currentTile == null) return;

        if (_sourcePickerDialog == null)
        {
            _sourcePickerDialog = new AcceptDialog
            {
                Title = "Select Atlas Source",
                InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
                Size = GetLargeDialogSize(),
                OkButtonText = "Select"
            };

            var dialogVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };

            // Zoom slider row
            var zoomRow = new HBoxContainer();
            zoomRow.AddChild(new Label { Text = "Zoom:" });
            _sourcePickerZoomSlider = new HSlider
            {
                MinValue = 0.5,
                MaxValue = 5.0,
                Step = 0.1,
                Value = 1.0,
                CustomMinimumSize = new Vector2(150, 0),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _sourcePickerZoomSlider.ValueChanged += OnSourcePickerZoomChanged;
            zoomRow.AddChild(_sourcePickerZoomSlider);
            var zoomLabel = new Label { Text = "100%" };
            _sourcePickerZoomSlider.ValueChanged += (val) => zoomLabel.Text = $"{(int)(val * 100)}%";
            zoomRow.AddChild(zoomLabel);
            dialogVBox.AddChild(zoomRow);

            // Filter row
            var filterRow = new HBoxContainer();
            filterRow.AddChild(new Label { Text = "Filter:" });
            _sourcePickerFilterField = new LineEdit
            {
                PlaceholderText = "Filter sources...",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClearButtonEnabled = true
            };
            _sourcePickerFilterField.TextChanged += OnSourcePickerFilterChanged;
            filterRow.AddChild(_sourcePickerFilterField);
            dialogVBox.AddChild(filterRow);

            var scroll = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            scroll.GuiInput += OnSourcePickerScrollInput;

            var grid = new GridContainer
            {
                Columns = (int)(2f/_sourcePickerZoomSlider.Value),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            grid.AddThemeConstantOverride("h_separation", 12);
            grid.AddThemeConstantOverride("v_separation", 12);

            _sourcePanelsBySourceId = new Dictionary<int, PanelContainer>();
            _sourceDisplayNames = new Dictionary<int, string>();
            _sourcePickerThumbnails = new List<TextureRect>();
            var sources = _service.GetAvailableAtlasSources();

            foreach (var sourceInfo in sources)
            {
                var panel = new PanelContainer
                {
                    CustomMinimumSize = new Vector2(280, 300),
                    MouseFilter = MouseFilterEnum.Pass
                };

                var vbox = new VBoxContainer
                {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    SizeFlagsVertical = SizeFlags.ExpandFill
                };

                var thumbnail = new TextureRect
                {
                    CustomMinimumSize = new Vector2(256, 256),
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                    TextureFilter = TextureFilterEnum.Nearest,
                    SizeFlagsHorizontal = SizeFlags.ShrinkCenter
                };
                _sourcePickerThumbnails.Add(thumbnail);

                // Show entire atlas texture
                if (sourceInfo.Source?.Texture != null)
                {
                    thumbnail.Texture = sourceInfo.Source.Texture;
                }

                vbox.AddChild(thumbnail);
                var label = new Label
                {
                    Text = sourceInfo.DisplayName,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                label.AddThemeFontSizeOverride("font_size", 10);
                vbox.AddChild(label);

                panel.AddChild(vbox);
                int capturedSourceId = sourceInfo.SourceId;
                panel.GuiInput += (evt) => OnSourcePanelClicked(capturedSourceId, evt);
                _sourcePanelsBySourceId[sourceInfo.SourceId] = panel;
                _sourceDisplayNames[sourceInfo.SourceId] = sourceInfo.DisplayName;
                grid.AddChild(panel);
            }

            scroll.AddChild(grid);
            dialogVBox.AddChild(scroll);
            _sourcePickerDialog.AddChild(dialogVBox);
            AddChild(_sourcePickerDialog);
            _sourcePickerDialog.Confirmed += OnSourcePickerConfirmed;
        }

        _selectedSourceIdForPicker = _currentTile.SourceId;
        UpdateSourcePanelHighlights();

        // Reset filter and show all sources
        if (_sourcePickerFilterField != null)
        {
            _sourcePickerFilterField.Text = "";
            OnSourcePickerFilterChanged("");
        }

        _sourcePickerDialog.Popup();
    }

    private void OnSourcePanelClicked(int sourceId, InputEvent evt)
    {
        if (evt is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.ButtonIndex == MouseButton.Left)
        {
            _selectedSourceIdForPicker = sourceId;
            UpdateSourcePanelHighlights();
        }
    }

    private void UpdateSourcePanelHighlights()
    {
        if (_sourcePanelsBySourceId == null) return;

        foreach (var (sourceId, panel) in _sourcePanelsBySourceId)
        {
            if (sourceId == _selectedSourceIdForPicker)
            {
                panel.Modulate = new Color(1.2f, 1.2f, 1.0f);
            }
            else
            {
                panel.Modulate = Colors.White;
            }
        }
    }

    private void OnSourcePickerZoomChanged(double value)
    {
        _sourcePickerZoom = (float)value;
        UpdateSourcePickerThumbnailSizes();
    }

    private void OnSourcePickerScrollInput(InputEvent evt)
    {
        if (evt is InputEventMouseButton mouseBtn && mouseBtn.Pressed && mouseBtn.CtrlPressed)
        {
            if (mouseBtn.ButtonIndex == MouseButton.WheelUp)
            {
                _sourcePickerZoom = Mathf.Min(_sourcePickerZoom + 0.1f, 2.0f);
                if (_sourcePickerZoomSlider != null)
                    _sourcePickerZoomSlider.Value = _sourcePickerZoom;
                UpdateSourcePickerThumbnailSizes();
            }
            else if (mouseBtn.ButtonIndex == MouseButton.WheelDown)
            {
                _sourcePickerZoom = Mathf.Max(_sourcePickerZoom - 0.1f, 0.5f);
                if (_sourcePickerZoomSlider != null)
                    _sourcePickerZoomSlider.Value = _sourcePickerZoom;
                UpdateSourcePickerThumbnailSizes();
            }
        }
    }

    private void UpdateSourcePickerThumbnailSizes()
    {
        if (_sourcePickerThumbnails == null || _sourcePanelsBySourceId == null) return;

        var baseSize = 256f;
        var basePanelWidth = 280f;
        var basePanelHeight = 300f;

        var newSize = new Vector2(baseSize * _sourcePickerZoom, baseSize * _sourcePickerZoom);
        var newPanelSize = new Vector2(basePanelWidth * _sourcePickerZoom, basePanelHeight * _sourcePickerZoom);

        foreach (var thumbnail in _sourcePickerThumbnails)
        {
            thumbnail.CustomMinimumSize = newSize;
        }

        foreach (var panel in _sourcePanelsBySourceId.Values)
        {
            panel.CustomMinimumSize = newPanelSize;
        }
    }

    private void OnSourcePickerFilterChanged(string filterText)
    {
        if (_sourcePanelsBySourceId == null || _sourceDisplayNames == null) return;

        var filter = filterText.ToLowerInvariant();

        foreach (var (sourceId, panel) in _sourcePanelsBySourceId)
        {
            if (string.IsNullOrEmpty(filter))
            {
                panel.Visible = true;
            }
            else
            {
                var displayName = _sourceDisplayNames.GetValueOrDefault(sourceId, "");
                panel.Visible = displayName.Contains(filter, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private void OnSourcePickerConfirmed()
    {
        if (_currentTile == null) return;

        _currentTile.SourceId = _selectedSourceIdForPicker;
        _sourceIdField!.Value = _selectedSourceIdForPicker;

        // Update dropdown to match
        for (int i = 0; i < _sourceDropdown!.ItemCount; i++)
        {
            if (_sourceDropdown.GetItemId(i) == _selectedSourceIdForPicker)
            {
                _sourceDropdown.Selected = i;
                break;
            }
        }

        UpdateSourceButtonText();
        UpdateAtlasButtonAppearance();
        _service.UpdateTile(_currentTile);
    }

    private void OnTileModeChanged(long index)
    {
        if (_isUpdating || _currentTile == null) return;

        _currentTile.TileMode = index switch
        {
            1 => "pertilevariations",
            2 => "permapvariations",
            3 => "autotile",
            4 => "permapvariationautotile",
            5 => "animated",
            _ => "plain"
        };

        UpdateSectionVisibility();
        _service.UpdateTile(_currentTile);
    }

    private void UpdateSectionVisibility()
    {
        if (_currentTile == null) return;

        var mode = _currentTile.TileMode?.ToLowerInvariant() ?? "plain";

        // Determine which sections to show based on mode
        var showAutoTile = mode is "autotile" or "permapvariationautotile";
        var showVariations = mode is "pertilevariations" or "permapvariations" or "permapvariationautotile";
        var showAnimation = mode == "animated";

        // Toggle foldout visibility
        _autoTileFoldout!.Visible = showAutoTile;
        _variationsFoldout!.Visible = showVariations;
        _animationFoldout!.Visible = showAnimation;

        // Update variation mode dropdown based on tile mode
        if (showVariations && _variationModeDropdown != null)
        {
            _isUpdating = true;
            if (mode is "permapvariations" or "permapvariationautotile")
            {
                _variationModeDropdown.Selected = 1; // Per Generation
                _currentTile.VariationMode = "pergeneration";
            }
            else if (mode == "pertilevariations")
            {
                _variationModeDropdown.Selected = 0; // Per Instance
                _currentTile.VariationMode = "perinstance";
            }
            _isUpdating = false;
        }
    }

    private void OpenVariantPickerDialog(int variantIndex)
    {
        if (_currentTile == null) return;

        _editingVariantIndex = variantIndex;

        // Ensure AutoTileVariants is properly sized for the current variant count
        if (_currentTile.AutoTileVariants == null || _currentTile.AutoTileVariants.Length < _currentVariantCount)
        {
            var newArray = new Vector2I?[_currentVariantCount];
            if (_currentTile.AutoTileVariants != null)
            {
                Array.Copy(_currentTile.AutoTileVariants, newArray, _currentTile.AutoTileVariants.Length);
            }
            _currentTile.AutoTileVariants = newArray;
        }

        // Create dialog lazily
        if (_variantPickerDialog == null)
        {
            _variantPickerDialog = new AcceptDialog
            {
                Title = "Select Atlas Variant",
                InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
                Size = GetLargeDialogSize(),
                OkButtonText = "Assign",
            };

            var dialogVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };

            var pickerScroll = new ScrollContainer
            {
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
            _variantPicker.SetSource(source, tileSize, _currentTile.SourceId, _currentTile.SourceScale);

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

        // Generate title based on format
        string maskLabel;
        if (_currentVariantCount == 47)
        {
            var blobMasks = CardCleaner.Scripts.Features.Worldgen.AutoTiling.NeighborBitmask8.GetValid47Masks();
            maskLabel = GetBlobMaskLabel(variantIndex, blobMasks[variantIndex]);
        }
        else
        {
            maskLabel = CornerBitmaskLabels[variantIndex];
        }
        _variantPickerDialog.Title = $"Select Variant for Bitmask {variantIndex}: {maskLabel}";
        _variantPickerDialog.Popup();
    }

    private void OnVariantPickerConfirmed()
    {
        if (_editingVariantIndex < 0 || _currentTile == null || _variantPicker == null) return;

        _currentTile.AutoTileVariants ??= new Vector2I?[_currentVariantCount];
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
        for (int i = 0; i < _currentVariantCount; i++)
        {
            UpdateVariantThumbnail(i);
        }
        _service.UpdateTile(_currentTile);
    }

    private void UpdateVariantThumbnail(int index)
    {
        if (_currentTile == null || _variantThumbnails == null ||
            index >= _variantThumbnails.Length || _variantThumbnails[index] == null) return;

        var thumbnail = _variantThumbnails[index]!;

        // Check if variant is defined
        if (_currentTile.AutoTileVariants == null ||
            index >= _currentTile.AutoTileVariants.Length ||
            !_currentTile.AutoTileVariants[index].HasValue)
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

        var baseTileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
        // Account for source scale: 0.5x = 32px source, 2.0x = 8px source
        var actualTileSize = new Vector2I(
            (int)(baseTileSize.X / _currentTile.SourceScale),
            (int)(baseTileSize.Y / _currentTile.SourceScale)
        );
        var region = new Rect2I(coords * actualTileSize, actualTileSize);

        var atlasTex = new AtlasTexture
        {
            Atlas = texture,
            Region = region
        };
        thumbnail.Texture = atlasTex;
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
    /// Gets a dialog size that fills approximately 70% of the screen.
    /// Used for picker dialogs to provide ample space for browsing large atlases.
    /// </summary>
    private Vector2I GetLargeDialogSize()
    {
        const float fillPercent = 0.70f;
        const int minWidth = 800;
        const int minHeight = 600;
        const int maxWidth = 1920;
        const int maxHeight = 1200;

        // Get screen size from the editor window
        var screenSize = DisplayServer.ScreenGetSize();

        var width = (int)(screenSize.X * fillPercent);
        var height = (int)(screenSize.Y * fillPercent);

        // Clamp to reasonable bounds
        width = Mathf.Clamp(width, minWidth, maxWidth);
        height = Mathf.Clamp(height, minHeight, maxHeight);

        return new Vector2I(width, height);
    }

    private void OnAutoTileFormatChanged(long index)
    {
        if (_isUpdating || _currentTile == null || _autoTileFormatDropdown == null) return;

        // Get format name from dropdown metadata
        var newFormat = _autoTileFormatDropdown.GetItemMetadata((int)index).AsString();
        if (string.IsNullOrEmpty(newFormat)) newFormat = "corner16";

        if (string.Equals(_currentTile.AutoTileFormat, newFormat, StringComparison.OrdinalIgnoreCase)) return;

        _currentTile.AutoTileFormat = newFormat;
        UpdateFormatDescription(newFormat);

        // Get variant count from format definition
        var format = _service.GetFormatDefinition(newFormat);
        var variantCount = format?.AllowedBitmasks.Count ?? 16;

        // Resize the variants array
        if (_currentTile.AutoTileVariants != null)
        {
            var oldVariants = _currentTile.AutoTileVariants;
            _currentTile.AutoTileVariants = new Godot.Vector2I?[variantCount];
            // Copy what we can (format change may lose data)
            Array.Copy(oldVariants, _currentTile.AutoTileVariants, Math.Min(oldVariants.Length, variantCount));
        }

        // Clear custom variant definitions when format changes
        _currentTile.CustomVariantDefinitions?.Clear();

        RebuildVariantGrid(variantCount, newFormat);
        _service.UpdateTile(_currentTile);
    }

    private void PopulateTerrainDropdowns()
    {
        if (_innerTerrainDropdown == null || _outerTerrainDropdown == null) return;

        _innerTerrainDropdown.Clear();
        _outerTerrainDropdown.Clear();

        // Get all terrain layer tiles (non-auto-tiles)
        var terrainTiles = _service.AllTiles
            .Where(t => t.Layer == "terrain" && !t.HasAutoTileVariants)
            .OrderBy(t => t.Name)
            .ToList();

        // Inner terrain: (none/default) + all terrain tiles
        _innerTerrainDropdown.AddItem("(Default - use this tile's ID)", 0);
        for (int i = 0; i < terrainTiles.Count; i++)
        {
            _innerTerrainDropdown.AddItem($"{terrainTiles[i].Name} ({terrainTiles[i].Id})", i + 1);
        }

        // Outer terrain: (none) + * (compositable) + all terrain tiles
        _outerTerrainDropdown.AddItem("(None - no transition)", 0);
        _outerTerrainDropdown.AddItem("* (Compositable - transparent)", 1);
        for (int i = 0; i < terrainTiles.Count; i++)
        {
            _outerTerrainDropdown.AddItem($"{terrainTiles[i].Name} ({terrainTiles[i].Id})", i + 2);
        }
    }

    private void OnInnerTerrainChanged(long index)
    {
        if (_isUpdating || _currentTile == null) return;

        if (index == 0)
        {
            // Default - clear inner terrain
            _currentTile.InnerTerrainId = null;
        }
        else
        {
            // Get terrain tiles in same order as populated
            var terrainTiles = _service.AllTiles
                .Where(t => t.Layer == "terrain" && !t.HasAutoTileVariants)
                .OrderBy(t => t.Name)
                .ToList();

            if (index - 1 < terrainTiles.Count)
            {
                _currentTile.InnerTerrainId = terrainTiles[(int)index - 1].Id;
            }
        }

        _service.UpdateTile(_currentTile);
    }

    private void OnOuterTerrainChanged(long index)
    {
        if (_isUpdating || _currentTile == null) return;

        if (index == 0)
        {
            // None - clear outer terrain
            _currentTile.OuterTerrainId = null;
        }
        else if (index == 1)
        {
            // Compositable
            _currentTile.OuterTerrainId = "*";
        }
        else
        {
            // Get terrain tiles in same order as populated
            var terrainTiles = _service.AllTiles
                .Where(t => t.Layer == "terrain" && !t.HasAutoTileVariants)
                .OrderBy(t => t.Name)
                .ToList();

            if (index - 2 < terrainTiles.Count)
            {
                _currentTile.OuterTerrainId = terrainTiles[(int)index - 2].Id;
            }
        }

        _service.UpdateTile(_currentTile);
    }

    private void RebuildVariantGrid(int variantCount, string format)
    {
        if (_variantGridContainer == null) return;

        // Clear existing grid
        foreach (var child in _variantGridContainer.GetChildren())
        {
            child.QueueFree();
        }

        _currentVariantCount = variantCount;
        _variantThumbnails = new TextureRect?[variantCount];
        _variantShapePreviews = new TileShapePreview?[variantCount];

        // Get the 47 valid blob masks if needed
        var blobMasks = format == "blob47"
            ? CardCleaner.Scripts.Features.Worldgen.AutoTiling.NeighborBitmask8.GetValid47Masks()
            : null;

        _variantGrid = new GridContainer
        {
            Columns = 4,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _variantGrid.AddThemeConstantOverride("h_separation", 4);
        _variantGrid.AddThemeConstantOverride("v_separation", 4);
        _variantGridContainer.AddChild(_variantGrid);

        for (int i = 0; i < variantCount; i++)
        {
            var slotContainer = new VBoxContainer
            {
                CustomMinimumSize = new Vector2(80, 130),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };

            // Label with bitmask info
            string labelText;
            int maskValue;
            if (format == "blob47" && blobMasks != null)
            {
                maskValue = blobMasks[i];
                labelText = $"{i}: {GetBlobMaskLabel(i, maskValue)}";
            }
            else if (format == "edge16")
            {
                maskValue = i;
                labelText = $"{i}: {EdgeBitmaskLabels[i]}";
            }
            else
            {
                maskValue = i;
                labelText = $"{i}: {CornerBitmaskLabels[i]}";
            }

            var label = new Label
            {
                Text = labelText,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeFontSizeOverride("font_size", 8);
            slotContainer.AddChild(label);

            // Shape preview
            var shapePreview = new TileShapePreview
            {
                CustomMinimumSize = new Vector2(24, 24),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            var shapeFormat = format switch
            {
                "blob47" => TileShapePreview.Format.Blob47,
                "edge16" => TileShapePreview.Format.Edge16,
                _ => TileShapePreview.Format.Corner16
            };
            shapePreview.SetMask(maskValue, shapeFormat);
            slotContainer.AddChild(shapePreview);
            _variantShapePreviews[i] = shapePreview;

            // Thumbnail
            var thumbnailPanel = new PanelContainer
            {
                CustomMinimumSize = new Vector2(64, 64),
                SizeFlagsHorizontal = SizeFlags.ShrinkCenter
            };
            var thumbnail = new TextureRect
            {
                CustomMinimumSize = new Vector2(64, 64),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                TextureFilter = TextureFilterEnum.Nearest
            };
            thumbnailPanel.AddChild(thumbnail);
            slotContainer.AddChild(thumbnailPanel);
            _variantThumbnails[i] = thumbnail;

            // Buttons
            var buttonRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
            var selectBtn = new Button
            {
                Text = "Set",
                CustomMinimumSize = new Vector2(28, 0)
            };
            selectBtn.AddThemeFontSizeOverride("font_size", 10);
            int index = i;
            selectBtn.Pressed += () => OpenVariantPickerDialog(index);
            buttonRow.AddChild(selectBtn);

            var clearBtn = new Button
            {
                Text = "X",
                CustomMinimumSize = new Vector2(20, 0),
                TooltipText = "Clear variant"
            };
            clearBtn.AddThemeFontSizeOverride("font_size", 10);
            clearBtn.Pressed += () => ClearVariant(index);
            buttonRow.AddChild(clearBtn);
            slotContainer.AddChild(buttonRow);

            _variantGrid.AddChild(slotContainer);
        }

        // Refresh thumbnails if we have a tile selected
        if (_currentTile != null)
        {
            for (int i = 0; i < variantCount; i++)
            {
                UpdateVariantThumbnail(i);
            }
        }
    }

    private void OnVariationModeChanged(long index)
    {
        if (_isUpdating || _currentTile == null) return;

        _currentTile.VariationMode = index switch
        {
            1 => "pergeneration",
            _ => "perinstance"
        };

        _service.UpdateTile(_currentTile);
    }

    private void RebuildVariationsList()
    {
        if (_variationsListContainer == null || _currentTile == null) return;

        // Clear existing entries
        foreach (var child in _variationsListContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Add base tile entry (informational)
        var baseRow = new HBoxContainer();
        var baseThumbnail = CreateVariationThumbnail(
            new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY));
        baseRow.AddChild(baseThumbnail);
        baseRow.AddChild(new Label
        {
            Text = $"Base: ({_currentTile.AtlasX}, {_currentTile.AtlasY})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        _variationsListContainer.AddChild(baseRow);

        // Add variation entries
        if (_currentTile.Variations != null)
        {
            for (int i = 0; i < _currentTile.Variations.Length; i++)
            {
                var variation = _currentTile.Variations[i];
                var row = CreateVariationRow(i, variation);
                _variationsListContainer.AddChild(row);
            }
        }
    }

    private HBoxContainer CreateVariationRow(int index, Vector2I coords)
    {
        var row = new HBoxContainer();

        var thumbnail = CreateVariationThumbnail(coords);
        row.AddChild(thumbnail);

        row.AddChild(new Label
        {
            Text = $"Var {index + 1}: ({coords.X}, {coords.Y})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        var removeBtn = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(24, 0),
            TooltipText = "Remove variation"
        };
        int capturedIndex = index;
        removeBtn.Pressed += () => RemoveVariation(capturedIndex);
        row.AddChild(removeBtn);

        return row;
    }

    private PanelContainer CreateVariationThumbnail(Vector2I coords)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(32, 32)
        };

        var textureRect = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };

        if (_currentTile != null)
        {
            var texture = _service.GetTileTexture(_currentTile);
            if (texture != null)
            {
                var baseTileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
                // Account for source scale: 0.5x = 32px source, 2.0x = 8px source
                var actualTileSize = new Vector2I(
                    (int)(baseTileSize.X / _currentTile.SourceScale),
                    (int)(baseTileSize.Y / _currentTile.SourceScale)
                );
                var region = new Rect2I(coords * actualTileSize, actualTileSize);
                var atlasTex = new AtlasTexture
                {
                    Atlas = texture,
                    Region = region
                };
                textureRect.Texture = atlasTex;
            }
        }

        panel.AddChild(textureRect);
        return panel;
    }

    private void OpenAddVariationDialog()
    {
        if (_currentTile == null) return;

        // Create dialog lazily
        if (_variationPickerDialog == null)
        {
            _variationPickerDialog = new AcceptDialog
            {
                Title = "Add Tile Variation",
                InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
                Size = GetLargeDialogSize(),
                OkButtonText = "Add"
            };

            var dialogVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };

            var pickerScroll = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Auto
            };

            _variationPicker = new TilesetAtlasPicker();
            pickerScroll.AddChild(_variationPicker);
            dialogVBox.AddChild(pickerScroll);

            _variationPickerDialog.AddChild(dialogVBox);
            AddChild(_variationPickerDialog);

            _variationPickerDialog.Confirmed += OnAddVariationConfirmed;
        }

        // Configure picker with current tile's atlas source
        var source = _service.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _variationPicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _variationPicker.SetSource(source, tileSize, _currentTile.SourceId, _currentTile.SourceScale);
            _variationPicker.SelectedCoords = new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
        }

        _variationPickerDialog.Popup();
    }

    private void OnAddVariationConfirmed()
    {
        if (_currentTile == null || _variationPicker == null) return;

        var newCoords = _variationPicker.SelectedCoords;

        // Don't add if same as base coords
        if (newCoords.X == _currentTile.AtlasX && newCoords.Y == _currentTile.AtlasY)
            return;

        // Don't add duplicates
        if (_currentTile.Variations != null)
        {
            foreach (var v in _currentTile.Variations)
            {
                if (v.X == newCoords.X && v.Y == newCoords.Y)
                    return;
            }
        }

        // Add to variations array
        var existing = _currentTile.Variations ?? Array.Empty<Vector2I>();
        var newVariations = new Vector2I[existing.Length + 1];
        Array.Copy(existing, newVariations, existing.Length);
        newVariations[existing.Length] = newCoords;
        _currentTile.Variations = newVariations;

        RebuildVariationsList();
        _service.UpdateTile(_currentTile);
    }

    private void RemoveVariation(int index)
    {
        if (_currentTile?.Variations == null || index >= _currentTile.Variations.Length)
            return;

        var oldVariations = _currentTile.Variations;
        if (oldVariations.Length == 1)
        {
            _currentTile.Variations = null;
        }
        else
        {
            var newVariations = new Vector2I[oldVariations.Length - 1];
            int newIdx = 0;
            for (int i = 0; i < oldVariations.Length; i++)
            {
                if (i != index)
                    newVariations[newIdx++] = oldVariations[i];
            }
            _currentTile.Variations = newVariations;
        }

        RebuildVariationsList();
        _service.UpdateTile(_currentTile);
    }

    private void OnAnimationDurationChanged(double value)
    {
        if (_isUpdating || _currentTile == null) return;

        _currentTile.AnimationFrameDuration = (float)value;
        _service.UpdateTile(_currentTile);
    }

    private void RebuildAnimationFramesList()
    {
        if (_animationFramesContainer == null || _currentTile == null) return;

        // Clear existing entries
        foreach (var child in _animationFramesContainer.GetChildren())
        {
            child.QueueFree();
        }

        // Add base tile as frame 0 (informational)
        var baseRow = new HBoxContainer();
        var baseThumbnail = CreateVariationThumbnail(
            new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY));
        baseRow.AddChild(baseThumbnail);
        baseRow.AddChild(new Label
        {
            Text = $"Frame 0 (Base): ({_currentTile.AtlasX}, {_currentTile.AtlasY})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });
        _animationFramesContainer.AddChild(baseRow);

        // Add additional frame entries
        if (_currentTile.AnimationFrames != null)
        {
            for (int i = 0; i < _currentTile.AnimationFrames.Length; i++)
            {
                var frame = _currentTile.AnimationFrames[i];
                var row = CreateAnimationFrameRow(i, frame);
                _animationFramesContainer.AddChild(row);
            }
        }
    }

    private HBoxContainer CreateAnimationFrameRow(int index, Vector2I coords)
    {
        var row = new HBoxContainer();

        var thumbnail = CreateVariationThumbnail(coords);
        row.AddChild(thumbnail);

        row.AddChild(new Label
        {
            Text = $"Frame {index + 1}: ({coords.X}, {coords.Y})",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        });

        var removeBtn = new Button
        {
            Text = "X",
            CustomMinimumSize = new Vector2(24, 0),
            TooltipText = "Remove frame"
        };
        int capturedIndex = index;
        removeBtn.Pressed += () => RemoveAnimationFrame(capturedIndex);
        row.AddChild(removeBtn);

        return row;
    }

    private void OpenAddAnimationFrameDialog()
    {
        if (_currentTile == null) return;

        // Create dialog lazily
        if (_animationFramePickerDialog == null)
        {
            _animationFramePickerDialog = new AcceptDialog
            {
                Title = "Add Animation Frame",
                InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
                Size = GetLargeDialogSize(),
                OkButtonText = "Add"
            };

            var dialogVBox = new VBoxContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };

            var pickerScroll = new ScrollContainer
            {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollMode.Auto,
                VerticalScrollMode = ScrollMode.Auto
            };

            _animationFramePicker = new TilesetAtlasPicker();
            pickerScroll.AddChild(_animationFramePicker);
            dialogVBox.AddChild(pickerScroll);

            _animationFramePickerDialog.AddChild(dialogVBox);
            AddChild(_animationFramePickerDialog);

            _animationFramePickerDialog.Confirmed += OnAddAnimationFrameConfirmed;
        }

        // Configure picker with current tile's atlas source
        var source = _service.GetAtlasSource(_currentTile.SourceId);
        if (source != null && _animationFramePicker != null)
        {
            var tileSize = _service.TileSet?.TileSize ?? new Vector2I(16, 16);
            _animationFramePicker.SetSource(source, tileSize, _currentTile.SourceId, _currentTile.SourceScale);
            _animationFramePicker.SelectedCoords = new Vector2I(_currentTile.AtlasX, _currentTile.AtlasY);
        }

        _animationFramePickerDialog.Popup();
    }

    private void OnAddAnimationFrameConfirmed()
    {
        if (_currentTile == null || _animationFramePicker == null) return;

        var newCoords = _animationFramePicker.SelectedCoords;

        // Add to animation frames array
        var existing = _currentTile.AnimationFrames ?? Array.Empty<Vector2I>();
        var newFrames = new Vector2I[existing.Length + 1];
        Array.Copy(existing, newFrames, existing.Length);
        newFrames[existing.Length] = newCoords;
        _currentTile.AnimationFrames = newFrames;

        RebuildAnimationFramesList();
        _service.UpdateTile(_currentTile);
    }

    private void RemoveAnimationFrame(int index)
    {
        if (_currentTile?.AnimationFrames == null || index >= _currentTile.AnimationFrames.Length)
            return;

        var oldFrames = _currentTile.AnimationFrames;
        if (oldFrames.Length == 1)
        {
            _currentTile.AnimationFrames = null;
        }
        else
        {
            var newFrames = new Vector2I[oldFrames.Length - 1];
            int newIdx = 0;
            for (int i = 0; i < oldFrames.Length; i++)
            {
                if (i != index)
                    newFrames[newIdx++] = oldFrames[i];
            }
            _currentTile.AnimationFrames = newFrames;
        }

        RebuildAnimationFramesList();
        _service.UpdateTile(_currentTile);
    }

    private void OnAdvancedVariantsToggled(bool pressed)
    {
        if (_isUpdating || _currentTile == null) return;

        _variantMappingEditor!.Visible = pressed;

        if (pressed)
        {
            // Configure the variant mapping editor with current tile and format
            var formatName = _currentTile.AutoTileFormat ?? "corner16";
            var format = _service.GetFormatDefinition(formatName);
            _variantMappingEditor.Configure(_currentTile, format, false);
        }
        else
        {
            // Clear custom variant definitions when disabling
            _currentTile.CustomVariantDefinitions?.Clear();
            _service.UpdateTile(_currentTile);
        }
    }

    private void OnVariantMappingsModified()
    {
        if (_isUpdating || _currentTile == null || _variantMappingEditor == null) return;

        // Copy variant definitions from the editor to the current tile
        var definitions = _variantMappingEditor.GetVariantDefinitions();
        _currentTile.CustomVariantDefinitions = definitions;

        // Also update AutoTileVariants for backward compatibility
        SyncVariantDefinitionsToAutoTileVariants();

        _service.UpdateTile(_currentTile);
    }

    private void SyncVariantDefinitionsToAutoTileVariants()
    {
        if (_currentTile?.CustomVariantDefinitions == null) return;

        // Ensure AutoTileVariants array exists
        _currentTile.AutoTileVariants ??= new Vector2I?[_currentVariantCount];

        foreach (var (bitmask, definition) in _currentTile.CustomVariantDefinitions)
        {
            if (bitmask < _currentTile.AutoTileVariants.Length)
            {
                _currentTile.AutoTileVariants[bitmask] = definition.AtlasCoords;
            }
        }
    }

    private void UpdateAdvancedVariantsUI()
    {
        if (_currentTile == null || _useAdvancedVariantsCheckbox == null || _variantMappingEditor == null)
            return;

        // Check if tile has custom variant definitions
        var hasAdvancedVariants = _currentTile.HasCustomVariantDefinitions;
        _useAdvancedVariantsCheckbox.ButtonPressed = hasAdvancedVariants;
        _variantMappingEditor.Visible = hasAdvancedVariants;

        if (hasAdvancedVariants)
        {
            var formatName = _currentTile.AutoTileFormat ?? "corner16";
            var format = _service.GetFormatDefinition(formatName);
            _variantMappingEditor.Configure(_currentTile, format, false);
        }
    }
}
#endif
