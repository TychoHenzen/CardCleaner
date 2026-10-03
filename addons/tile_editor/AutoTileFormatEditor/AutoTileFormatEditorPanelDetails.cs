#if TOOLS
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    private void SetupRightPanel()
    {
        CreateDetailsPanel();
        AddFormatIdentityFields();
        AddBitmaskConfiguration();
        AddVariantConfiguration();
    }

    private void CreateDetailsPanel()
    {
        _detailsPanel = new VBoxContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        AddChild(_detailsPanel);

        _noSelectionLabel = new Label
        {
            Text = "Select a format to view details",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _detailsPanel.AddChild(_noSelectionLabel);

        _formatDetailsContainer = new VBoxContainer
        {
            Visible = false,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _detailsPanel.AddChild(_formatDetailsContainer);
    }

    private void AddFormatIdentityFields()
    {
        var nameRow = CreateRow("Name:");
        _nameField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Editable = false
        };
        _nameField.TextChanged += OnNameChanged;
        nameRow.AddChild(_nameField);
        _formatDetailsContainer!.AddChild(nameRow);

        var typeRow = CreateRow("Bitmask Type:");
        _bitmaskTypeDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _bitmaskTypeDropdown.AddItem("Corner4 (4-bit corners)", (int)BitmaskType.Corner4);
        _bitmaskTypeDropdown.AddItem("Edge4 (4-bit edges)", (int)BitmaskType.Edge4);
        _bitmaskTypeDropdown.AddItem("Full8 (8-bit blob)", (int)BitmaskType.Full8);
        _bitmaskTypeDropdown.ItemSelected += OnBitmaskTypeChanged;
        typeRow.AddChild(_bitmaskTypeDropdown);
        _formatDetailsContainer.AddChild(typeRow);

        var countRow = CreateRow("Variant Count:");
        _variantCountLabel = new Label { Text = "0" };
        countRow.AddChild(_variantCountLabel);
        _formatDetailsContainer.AddChild(countRow);

        var builtInNote = new Label
        {
            Text = "",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        builtInNote.AddThemeFontSizeOverride("font_size", 11);
        builtInNote.Name = "BuiltInNote";
        _formatDetailsContainer.AddChild(builtInNote);
        _formatDetailsContainer.AddChild(new HSeparator());
    }

    private void AddBitmaskConfiguration()
    {
        var bitmaskHeader = new Label { Text = "Allowed Bitmasks" };
        bitmaskHeader.AddThemeFontSizeOverride("font_size", 14);
        _formatDetailsContainer!.AddChild(bitmaskHeader);

        var bitmaskInfo = new Label
        {
            Text = "Toggle which bitmask patterns are valid for this format:",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        bitmaskInfo.AddThemeFontSizeOverride("font_size", 11);
        _formatDetailsContainer.AddChild(bitmaskInfo);

        var selectRow = new HBoxContainer();
        var selectAllButton = new Button { Text = "Select All" };
        selectAllButton.Pressed += () => _bitmaskGrid?.SelectAll();
        selectRow.AddChild(selectAllButton);
        var deselectAllButton = new Button { Text = "Deselect All" };
        deselectAllButton.Pressed += () => _bitmaskGrid?.DeselectAll();
        selectRow.AddChild(deselectAllButton);
        _formatDetailsContainer.AddChild(selectRow);

        _bitmaskScrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _bitmaskGrid = new BitmaskConfigGrid();
        _bitmaskGrid.AllowedBitmasksChanged += OnAllowedBitmasksChanged;
        _bitmaskScrollContainer.AddChild(_bitmaskGrid);
        _formatDetailsContainer.AddChild(_bitmaskScrollContainer);
    }

    private void AddVariantConfiguration()
    {
        _formatDetailsContainer!.AddChild(new HSeparator());
        _variantConfigFoldout = new FoldoutContainer("Default Variant Size", true);
        _formatDetailsContainer.AddChild(_variantConfigFoldout);

        var variantInfo = new Label
        {
            Text =
                "Configure default size for each variant (for multi-cell tiles like tall platforms).\n"
                + "Offset and atlas coords are configured per-tile in the tile properties panel.",
            AutowrapMode = TextServer.AutowrapMode.Word,
            Modulate = new Color(0.8f, 0.8f, 0.8f)
        };
        variantInfo.AddThemeFontSizeOverride("font_size", 10);
        _variantConfigFoldout.Content.AddChild(variantInfo);

        var variantScrollContainer = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 200),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _variantConfigFoldout.Content.AddChild(variantScrollContainer);

        _variantConfigContainer = new VBoxContainer
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        variantScrollContainer.AddChild(_variantConfigContainer);
    }
}
#endif
