#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void BuildVariationsSection(VBoxContainer vbox)
    {
        vbox.AddChild(new HSeparator());
        _variationsFoldout = new FoldoutContainer("Tile Variations", false);
        vbox.AddChild(_variationsFoldout);
        var info = new Label
        {
            Text = "Add visual variations for this tile type:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        info.AddThemeFontSizeOverride("font_size", 11);
        _variationsFoldout.Content.AddChild(info);
        BuildVariationModeControl();
        BuildVariationListControl();
    }

    private void BuildVariationModeControl()
    {
        var row = CreateRow("Mode:");
        _variationModeDropdown = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _variationModeDropdown.AddItem("Per Instance (random each tile)", 0);
        _variationModeDropdown.AddItem("Per Generation (one for whole map)", 1);
        _variationModeDropdown.ItemSelected += OnVariationModeChanged;
        row.AddChild(_variationModeDropdown);
        _variationsFoldout!.Content.AddChild(row);
        var note = new Label
        {
            Text = "Per Instance: Each tile placement uses random variant.\n"
                + "Per Generation: One variant chosen at map start.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        note.AddThemeFontSizeOverride("font_size", 9);
        _variationsFoldout.Content.AddChild(note);
    }

    private void BuildVariationListControl()
    {
        _variationsListContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _variationsFoldout!.Content.AddChild(_variationsListContainer);
        var button = new Button
        {
            Text = "+ Add Variation",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        button.Pressed += OpenAddVariationDialog;
        _variationsFoldout.Content.AddChild(button);
    }

    private void BuildAnimationSection(VBoxContainer vbox)
    {
        vbox.AddChild(new HSeparator());
        _animationFoldout = new FoldoutContainer("Tile Animation", false);
        vbox.AddChild(_animationFoldout);
        var info = new Label
        {
            Text = "Configure animation frames for this tile:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        info.AddThemeFontSizeOverride("font_size", 11);
        _animationFoldout.Content.AddChild(info);
        BuildAnimationDurationControl();
        BuildAnimationListControl();
    }

    private void BuildAnimationDurationControl()
    {
        var row = CreateRow("Frame Duration:");
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
        row.AddChild(_animationFrameDurationField);
        _animationFoldout!.Content.AddChild(row);
    }

    private void BuildAnimationListControl()
    {
        _animationFramesContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _animationFoldout!.Content.AddChild(_animationFramesContainer);
        var button = new Button
        {
            Text = "+ Add Frame",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        button.Pressed += OpenAddAnimationFrameDialog;
        _animationFoldout.Content.AddChild(button);
        var note = new Label
        {
            Text = "Note: Animation plays automatically in Godot's TileMap if configured in the TileSet.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        note.AddThemeFontSizeOverride("font_size", 9);
        _animationFoldout.Content.AddChild(note);
    }

    private void BuildDecorationSection(VBoxContainer vbox)
    {
        vbox.AddChild(new HSeparator());
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
        _decorationDensityRow.Visible = false;
        vbox.AddChild(_decorationDensityRow);
        var note = new Label
        {
            Text = "(Probability of decoration appearing in blob regions)",
            Modulate = new Color(0.7f, 0.7f, 0.7f),
            Visible = false
        };
        note.AddThemeFontSizeOverride("font_size", 11);
        vbox.AddChild(note);
    }

    private void BuildValidationSection(VBoxContainer vbox)
    {
        vbox.AddChild(new HSeparator());
        _validationLabel = new Label
        {
            Text = "",
            Modulate = new Color(1, 0.3f, 0.3f)
        };
        vbox.AddChild(_validationLabel);
    }
}
#endif
