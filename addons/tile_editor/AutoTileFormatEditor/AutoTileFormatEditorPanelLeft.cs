#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    private void SetupLeftPanel()
    {
        var leftVBox = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(180, 0)
        };
        AddChild(leftVBox);

        var header = new Label
        {
            Text = "Auto-Tile Formats",
            HorizontalAlignment = HorizontalAlignment.Center
        };
        header.AddThemeFontSizeOverride("font_size", 14);
        leftVBox.AddChild(header);
        leftVBox.AddChild(new HSeparator());

        _formatList = new ItemList
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SelectMode = ItemList.SelectModeEnum.Single
        };
        _formatList.ItemSelected += OnFormatSelected;
        leftVBox.AddChild(_formatList);

        var buttonRow = new HBoxContainer();
        var addButton = new Button
        {
            Text = "+ Add",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Create a new custom auto-tile format"
        };
        addButton.Pressed += OnAddPressed;
        buttonRow.AddChild(addButton);

        _deleteButton = new Button
        {
            Text = "Delete",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            TooltipText = "Delete the selected custom format",
            Disabled = true
        };
        _deleteButton.Pressed += OnDeletePressed;
        buttonRow.AddChild(_deleteButton);
        leftVBox.AddChild(buttonRow);
    }
}
#endif
