#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class TmxPreviewPanel
{
    public override void _Ready()
    {
        if (_service == null)
            return;

        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ExpandFill;
        AddHeader();
        AddPathSelector();
        AddAutoTileSelector();
        AddBaseTileSelector();
        AddFormatSelector();
        AddScaleAndOptions();
        AddPreview();
        CallDeferred(MethodName.TryAutoLoadTmx);
    }

    private void AddHeader()
    {
        var header = new Label
        {
            Text = "TMX Preview",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 16);
        AddChild(header);
        AddChild(new HSeparator());
    }

    private void AddPathSelector()
    {
        var row = CreateLabeledRow("TMX File:");
        _tmxPathEdit = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "Select a TMX file...",
            Editable = false
        };
        row.AddChild(_tmxPathEdit);
        _browseButton = new Button { Text = "Browse..." };
        _browseButton.Pressed += OnBrowsePressed;
        row.AddChild(_browseButton);
        _reloadButton = new Button
        {
            Text = "⟳",
            TooltipText = "Reload",
            Disabled = true
        };
        _reloadButton.Pressed += OnReloadPressed;
        row.AddChild(_reloadButton);
        AddChild(row);
    }

    private void AddAutoTileSelector()
    {
        var row = CreateLabeledRow("Auto-tile:");
        _autoTileSelector = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Select an auto-tile from the TMX to preview its variants interactively"
        };
        _autoTileSelector.ItemSelected += OnAutoTileSelected;
        row.AddChild(_autoTileSelector);
        AddChild(row);
    }

    private void AddBaseTileSelector()
    {
        var row = CreateLabeledRow("Base tile:");
        _baseTileSelector = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Base terrain for compositing transparent Wang tiles"
        };
        _baseTileSelector.ItemSelected += OnBaseTileSelected;
        row.AddChild(_baseTileSelector);
        AddChild(row);
        PopulateBaseTileSelector();
    }

    private void AddFormatSelector()
    {
        var row = CreateLabeledRow("Format:");
        _formatOverrideDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Override format for auto-tile preview (uses tile's format if not set)"
        };
        PopulateFormatDropdown();
        _formatOverrideDropdown.ItemSelected += OnFormatOverrideSelected;
        row.AddChild(_formatOverrideDropdown);
        AddChild(row);
    }

    private void AddScaleAndOptions()
    {
        var scaleRow = CreateLabeledRow("Scale:");
        _scaleSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 4,
            Step = 0.5,
            Value = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scaleSlider.ValueChanged += OnScaleChanged;
        scaleRow.AddChild(_scaleSlider);
        _scaleLabel = new Label
        {
            Text = "2x",
            CustomMinimumSize = new Vector2(30, 0)
        };
        scaleRow.AddChild(_scaleLabel);
        AddChild(scaleRow);

        _showDataGridCheckbox = new CheckBox
        {
            Text = "Show data grid overlay",
            ButtonPressed = true
        };
        _showDataGridCheckbox.Toggled += OnShowDataGridToggled;
        var optionsRow = new HBoxContainer();
        optionsRow.AddChild(_showDataGridCheckbox);
        AddChild(optionsRow);
        AddChild(new HSeparator());
    }

    private void AddPreview()
    {
        _infoLabel = new Label
        {
            Text = "Select a TMX file to preview how tiles resolve.",
            Modulate = new Color(0.8f, 0.8f, 0.8f),
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        AddChild(_infoLabel);

        var previewScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto
        };
        _previewControl = new TmxPreviewControl(_service!);
        _previewControl.InfoChanged += OnInfoChanged;
        previewScroll.AddChild(_previewControl);
        AddChild(previewScroll);
    }

    private static HBoxContainer CreateLabeledRow(string label)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(60, 0)
        });
        return row;
    }
}
#endif
