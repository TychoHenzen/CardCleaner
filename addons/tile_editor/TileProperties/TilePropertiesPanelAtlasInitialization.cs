#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TilePropertiesPanel
{
    private void BuildAtlasSection(VBoxContainer vbox)
    {
        vbox.AddChild(new HSeparator());
        var header = new Label { Text = "Atlas Coordinates" };
        header.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(header);
        BuildSourceSelector(vbox);
        BuildAtlasCoordinateSelector(vbox);
        _sourceIdField = new SpinBox
        {
            Visible = false,
            Value = 4,
            MinValue = 0,
            MaxValue = 10000
        };
        AddChild(_sourceIdField);
    }

    private void BuildSourceSelector(VBoxContainer vbox)
    {
        var row = CreateRow("Source:");
        _sourceDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Visible = false
        };
        _sourceDropdown.ItemSelected += OnSourceDropdownChanged;
        row.AddChild(_sourceDropdown);
        _sourcePickerButton = new Button
        {
            Text = "Click to select source...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _sourcePickerButton.Pressed += OpenSourcePickerDialog;
        row.AddChild(_sourcePickerButton);
        vbox.AddChild(row);
    }

    private void BuildAtlasCoordinateSelector(VBoxContainer vbox)
    {
        var row = CreateRow("Coords:");
        _atlasCoordButton = new Button { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        var content = new HBoxContainer();
        _atlasButtonThumbnail = new TextureRect
        {
            CustomMinimumSize = new Vector2(32, 32),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            TextureFilter = TextureFilterEnum.Nearest
        };
        content.AddChild(_atlasButtonThumbnail);
        _atlasButtonLabel = new Label
        {
            Text = "Click to select...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        content.AddChild(_atlasButtonLabel);
        _atlasCoordButton.AddChild(content);
        _atlasCoordButton.Pressed += OpenAtlasPickerDialog;
        row.AddChild(_atlasCoordButton);
        vbox.AddChild(row);
    }
}
#endif
