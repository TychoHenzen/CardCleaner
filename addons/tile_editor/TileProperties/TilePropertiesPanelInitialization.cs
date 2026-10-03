#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    public override void _Ready()
    {
        if (_service == null)
            return;

        _service.AutoTileFormatsLoaded += OnAutoTileFormatsLoaded;
        ConfigurePanelLayout();

        var vbox = CreateRootContainer();
        BuildHeader(vbox);
        BuildIdentityFields(vbox);
        BuildDescriptionField(vbox);
        BuildTileModeField(vbox);
        BuildGeneralProperties(vbox);
        BuildAtlasSection(vbox);
        BuildAutoTileSection(vbox);
        BuildVariationsSection(vbox);
        BuildAnimationSection(vbox);
        BuildDecorationSection(vbox);
        BuildValidationSection(vbox);
        SetFieldsEnabled(false);
    }

    private void ConfigurePanelLayout()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;
    }

    private VBoxContainer CreateRootContainer()
    {
        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(vbox);
        return vbox;
    }

    private static void BuildHeader(VBoxContainer vbox)
    {
        vbox.AddChild(new Label
        {
            Text = "Select a tile to edit its properties",
            HorizontalAlignment = HorizontalAlignment.Center
        });
        vbox.AddChild(new HSeparator());
    }

    private void BuildIdentityFields(VBoxContainer vbox)
    {
        var idRow = CreateRow("ID:");
        _idField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "tile_id (snake_case)"
        };
        _idField.TextChanged += OnFieldChanged;
        idRow.AddChild(_idField);
        vbox.AddChild(idRow);

        var nameRow = CreateRow("Name:");
        _nameField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "Display Name"
        };
        _nameField.TextChanged += OnFieldChanged;
        nameRow.AddChild(_nameField);
        vbox.AddChild(nameRow);
    }

    private void BuildDescriptionField(VBoxContainer vbox)
    {
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

        var note = new Label
        {
            Text = "(For artists and AI image generators)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        note.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(note);
    }

    private void BuildTileModeField(VBoxContainer vbox)
    {
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
    }

    private void BuildGeneralProperties(VBoxContainer vbox)
    {
        _generalFoldout = new FoldoutContainer("General Properties", false);
        vbox.AddChild(_generalFoldout);
        BuildGeneralFields();
        BuildSizeFields();
        BuildSourceScaleField();
        BuildBiomeFields();
    }

    private void BuildGeneralFields()
    {
        var passabilityRow = CreateRow("Passability:");
        _passabilityField = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _passabilityField.AddItem("Passable", 0);
        _passabilityField.AddItem("Solid", 1);
        _passabilityField.AddItem("Partially Passable", 2);
        _passabilityField.ItemSelected += _ => OnFieldChanged("");
        passabilityRow.AddChild(_passabilityField);
        _generalFoldout!.Content.AddChild(passabilityRow);

        var layerRow = CreateRow("Layer:");
        _layerField = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _layerField.AddItem("Terrain", 0);
        _layerField.AddItem("Decoration", 1);
        _layerField.AddItem("Structure", 2);
        _layerField.AddItem("Effects", 3);
        _layerField.ItemSelected += _ => OnFieldChanged("");
        layerRow.AddChild(_layerField);
        _generalFoldout.Content.AddChild(layerRow);

        AddElevationField();
        AddTransparencyField();
    }

    private void AddElevationField()
    {
        var row = CreateRow("Elevation:");
        _elevationField = new SpinBox
        {
            MinValue = -10,
            MaxValue = 10,
            Step = 0.1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _elevationField.ValueChanged += _ => OnFieldChanged("");
        row.AddChild(_elevationField);
        _generalFoldout!.Content.AddChild(row);
    }

    private void AddTransparencyField()
    {
        var row = CreateRow("Transparent:");
        _transparentField = new CheckBox();
        _transparentField.Toggled += _ => OnFieldChanged("");
        row.AddChild(_transparentField);
        _generalFoldout!.Content.AddChild(row);
    }

    private void BuildSizeFields()
    {
        var sizeHeader = new Label { Text = "Tile Size (cells)" };
        sizeHeader.AddThemeFontSizeOverride("font_size", 12);
        _generalFoldout!.Content.AddChild(sizeHeader);

        var sizeRow = CreateRow("Size:");
        var sizeBox = new HBoxContainer();
        sizeBox.AddChild(new Label { Text = "W:" });
        _sizeXField = CreateSizeField();
        sizeBox.AddChild(_sizeXField);
        sizeBox.AddChild(new Label { Text = "H:" });
        _sizeYField = CreateSizeField();
        sizeBox.AddChild(_sizeYField);
        sizeRow.AddChild(sizeBox);
        _generalFoldout.Content.AddChild(sizeRow);

        var note = new Label
        {
            Text = "(For multi-cell tiles like trees: 2x2, etc.)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        note.AddThemeFontSizeOverride("font_size", 11);
        _generalFoldout.Content.AddChild(note);
    }

    private SpinBox CreateSizeField()
    {
        var field = new SpinBox
        {
            MinValue = 1,
            MaxValue = 10,
            Step = 1,
            Value = 1,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        field.ValueChanged += _ => OnFieldChanged("");
        return field;
    }

    private void BuildSourceScaleField()
    {
        var row = CreateRow("Source Scale:");
        _sourceScaleDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sourceScaleDropdown.AddItem("0.5x (32px source)", 0);
        _sourceScaleDropdown.AddItem("1.0x (16px source)", 1);
        _sourceScaleDropdown.AddItem("2.0x (8px source)", 2);
        _sourceScaleDropdown.Selected = 1;
        _sourceScaleDropdown.ItemSelected += _ => OnFieldChanged("");
        row.AddChild(_sourceScaleDropdown);
        _generalFoldout!.Content.AddChild(row);

        var note = new Label
        {
            Text = "(For tiles from 8x8 or 32x32 source textures)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        note.AddThemeFontSizeOverride("font_size", 11);
        _generalFoldout.Content.AddChild(note);
    }

    private void BuildBiomeFields()
    {
        _generalFoldout!.Content.AddChild(new Label { Text = "Allowed Biomes:" });
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

        var note = new Label
        {
            Text = "(Leave all unchecked for universal tile)",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        note.AddThemeFontSizeOverride("font_size", 11);
        _generalFoldout.Content.AddChild(note);
    }
}
#endif
