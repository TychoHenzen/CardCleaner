#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTilePreviewPanel
{
    public override void _Ready()
    {
        if (_service == null)
            return;

        ConfigurePanelLayout();
        var mainVBox = CreateMainContainer();
        AddHeader(mainVBox);
        AddTileSelectors(mainVBox);
        AddFormatSelector(mainVBox);
        AddScaleSelector(mainVBox);
        AddOptions(mainVBox);
        AddPreview(mainVBox);
        CallDeferred(MethodName.PopulateDropdowns);
    }

    private void ConfigurePanelLayout()
    {
        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;
    }

    private VBoxContainer CreateMainContainer()
    {
        var container = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill
        };
        AddChild(container);
        return container;
    }

    private static void AddHeader(VBoxContainer container)
    {
        var header = new Label
        {
            Text = "Auto-Tile Preview (Dual Grid)",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 16);
        container.AddChild(header);
        container.AddChild(new HSeparator());
    }

    private void AddTileSelectors(VBoxContainer container)
    {
        var tileRow = CreateSelectorRow("Overlay tile:", out _tileSelector);
        _tileSelector!.ItemSelected += OnTileSelected;
        container.AddChild(tileRow);

        var baseRow = CreateSelectorRow("Base tile:", out _baseTileSelector);
        _baseTileSelector!.ItemSelected += OnBaseTileSelected;
        container.AddChild(baseRow);
    }

    private static HBoxContainer CreateSelectorRow(string label, out OptionButton selector)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(100, 0)
        });
        selector = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(selector);
        return row;
    }

    private void AddFormatSelector(VBoxContainer container)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = "Format:",
            CustomMinimumSize = new Vector2(100, 0)
        });
        _formatOverrideDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Override format for preview (uses tile's format if not set)"
        };
        PopulateFormatDropdown();
        _formatOverrideDropdown.ItemSelected += OnFormatOverrideSelected;
        row.AddChild(_formatOverrideDropdown);
        container.AddChild(row);
    }

    private void AddScaleSelector(VBoxContainer container)
    {
        var row = new HBoxContainer();
        row.AddChild(new Label
        {
            Text = "Scale:",
            CustomMinimumSize = new Vector2(100, 0)
        });
        _scaleSlider = new HSlider
        {
            MinValue = 1,
            MaxValue = 4,
            Step = 0.5,
            Value = 2,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _scaleSlider.ValueChanged += OnScaleChanged;
        row.AddChild(_scaleSlider);
        _scaleLabel = new Label
        {
            Text = "2x",
            CustomMinimumSize = new Vector2(30, 0)
        };
        row.AddChild(_scaleLabel);
        container.AddChild(row);
    }

    private void AddOptions(VBoxContainer container)
    {
        var optionsRow = new HBoxContainer();
        _showDataGridCheckbox = new CheckBox
        {
            Text = "Show data grid overlay",
            ButtonPressed = true
        };
        _showDataGridCheckbox.Toggled += OnShowDataGridToggled;
        optionsRow.AddChild(_showDataGridCheckbox);
        container.AddChild(optionsRow);
        container.AddChild(new HSeparator());
    }

    private void AddPreview(VBoxContainer container)
    {
        _infoLabel = new Label
        {
            Text = "Click cells to toggle. Visual tiles update automatically.",
            Modulate = new Color(0.8f, 0.8f, 0.8f),
            AutowrapMode = TextServer.AutowrapMode.Word
        };
        _infoLabel.AddThemeFontSizeOverride("font_size", 11);
        container.AddChild(_infoLabel);

        var previewScroll = new ScrollContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollMode.Auto,
            VerticalScrollMode = ScrollMode.Auto
        };
        _mapPreview = new DualGridAutoTilePreview(_service!);
        _mapPreview.InfoChanged += OnInfoChanged;
        previewScroll.AddChild(_mapPreview);
        container.AddChild(previewScroll);
    }
}
#endif
