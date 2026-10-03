#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class UnusedSourcesPanel
{
    public override void _Ready()
    {
        if (_service == null)
            return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        HorizontalScrollMode = ScrollMode.Disabled;
        _container = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_container);
        _summaryLabel = new Label
        {
            Text = "Loading...",
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _summaryLabel.AddThemeFontSizeOverride("font_size", 14);
        _container.AddChild(_summaryLabel);
        _container.AddChild(new HSeparator());
        AddActionButtons();
        _container.AddChild(new HSeparator());
    }

    private void AddActionButtons()
    {
        var buttonRow = new HBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _container!.AddChild(buttonRow);
        _compactIdsButton = new Button
        {
            Text = "Compact IDs",
            TooltipText =
                "Renumber atlas source IDs to be contiguous (0, 1, 2, ...) and update tiles.json",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _compactIdsButton.Pressed += OnCompactIdsPressed;
        buttonRow.AddChild(_compactIdsButton);
        _removeDuplicatesButton = new Button
        {
            Text = "Remove Duplicates",
            TooltipText =
                "Find sources with identical texture content and remove the ones with higher IDs",
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter
        };
        _removeDuplicatesButton.Pressed += OnRemoveDuplicatesPressed;
        buttonRow.AddChild(_removeDuplicatesButton);
    }
}
#endif
