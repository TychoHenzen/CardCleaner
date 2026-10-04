#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void BuildAutoTileSection(VBoxContainer vbox)
    {
        vbox.AddChild(new HSeparator());
        _autoTileFoldout = new FoldoutContainer("Auto-Tiling", false);
        vbox.AddChild(_autoTileFoldout);
        BuildFormatControls();
        BuildTerrainTransitionControls();
        BuildVariantControls();
        BuildAdvancedVariantControls();
    }

    private void BuildFormatControls()
    {
        var row = CreateRow("Format:");
        _autoTileFormatDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        PopulateFormatDropdown();
        _autoTileFormatDropdown.ItemSelected += OnAutoTileFormatChanged;
        row.AddChild(_autoTileFormatDropdown);
        _autoTileFoldout!.Content.AddChild(row);
        _formatDescriptionLabel = new Label
        {
            Text = "",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        _formatDescriptionLabel.AddThemeFontSizeOverride("font_size", 10);
        _autoTileFoldout.Content.AddChild(_formatDescriptionLabel);
        _autoTileFoldout.Content.AddChild(new HSeparator());
    }

    private void BuildTerrainTransitionControls()
    {
        var header = new Label { Text = "Terrain Transitions" };
        header.AddThemeFontSizeOverride("font_size", 12);
        _autoTileFoldout!.Content.AddChild(header);
        var info = new Label
        {
            Text = "Configure which terrains this auto-tile transitions between:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        info.AddThemeFontSizeOverride("font_size", 10);
        _autoTileFoldout.Content.AddChild(info);
        AddInnerTerrainControl();
        AddOuterTerrainControl();
        AddTerrainNote();
        _autoTileFoldout.Content.AddChild(new HSeparator());
    }

    private void AddInnerTerrainControl()
    {
        var row = CreateRow("Inner Terrain:");
        _innerTerrainDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "The terrain whose border is rendered. Defaults to this tile if not set."
        };
        _innerTerrainDropdown.ItemSelected += OnInnerTerrainChanged;
        row.AddChild(_innerTerrainDropdown);
        _autoTileFoldout!.Content.AddChild(row);
    }

    private void AddOuterTerrainControl()
    {
        var row = CreateRow("Outer Terrain:");
        _outerTerrainDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Background terrain. Use '*' for compositable (transparent, works with any base)."
        };
        _outerTerrainDropdown.ItemSelected += OnOuterTerrainChanged;
        row.AddChild(_outerTerrainDropdown);
        _autoTileFoldout!.Content.AddChild(row);
    }

    private void AddTerrainNote()
    {
        var note = new Label
        {
            Text = "'*' = Compositable (transparent border, composited onto base terrains at compile time)\n"
                + "Specific terrain = Fixed transition (pre-baked, only works with that terrain)",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.6f, 0.6f, 0.6f)
        };
        note.AddThemeFontSizeOverride("font_size", 9);
        _autoTileFoldout!.Content.AddChild(note);
    }

    private void BuildVariantControls()
    {
        var info = new Label
        {
            Text = "Assign atlas variants for each neighbor pattern:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        info.AddThemeFontSizeOverride("font_size", 11);
        _autoTileFoldout!.Content.AddChild(info);
        _variantGridContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _autoTileFoldout.Content.AddChild(_variantGridContainer);
        RebuildVariantGrid(16, "corner16");
        var clearButton = new Button
        {
            Text = "Clear All Variants",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        clearButton.Pressed += ClearAllVariants;
        _autoTileFoldout.Content.AddChild(clearButton);
        _autoTileFoldout.Content.AddChild(new HSeparator());
    }

    private void BuildAdvancedVariantControls()
    {
        _advancedVariantsFoldout = new FoldoutContainer(
            "Advanced Variant Configuration",
            true);
        _autoTileFoldout!.Content.AddChild(_advancedVariantsFoldout);
        var info = new Label
        {
            Text = "Configure multi-cell variants with custom sizes and offsets (e.g., 3-tile-tall walls):",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        info.AddThemeFontSizeOverride("font_size", 10);
        _advancedVariantsFoldout.Content.AddChild(info);
        AddAdvancedVariantEditor();
    }

    private void AddAdvancedVariantEditor()
    {
        var row = new HBoxContainer();
        _useAdvancedVariantsCheckbox = new CheckBox
        {
            Text = "Enable advanced variant definitions",
            TooltipText = "When enabled, allows configuring size and offset for each variant"
        };
        _useAdvancedVariantsCheckbox.Toggled += OnAdvancedVariantsToggled;
        row.AddChild(_useAdvancedVariantsCheckbox);
        _advancedVariantsFoldout!.Content.AddChild(row);
        _variantMappingEditor = new VariantMappingEditor(_service!);
        _variantMappingEditor.Visible = false;
        _variantMappingEditor.VariantsModified += OnVariantMappingsModified;
        _advancedVariantsFoldout.Content.AddChild(_variantMappingEditor);
    }
}
#endif
