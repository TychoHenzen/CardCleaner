#if TOOLS
using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    private void OnAddPressed()
    {
        if (_newFormatDialog == null)
            CreateNewFormatDialog();

        _newFormatNameField!.Text = "";
        _newFormatTypeDropdown!.Selected = 0;
        _newFormatDialog!.Popup();
    }

    private void CreateNewFormatDialog()
    {
        _newFormatDialog = new AcceptDialog
        {
            Title = "Create New Auto-Tile Format",
            InitialPosition = Window.WindowInitialPosition.CenterMainWindowScreen,
            Size = new Vector2I(400, 200),
            OkButtonText = "Create"
        };

        var vbox = new VBoxContainer();
        var nameRow = CreateRow("Name:");
        _newFormatNameField = new LineEdit
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            PlaceholderText = "my_custom_format"
        };
        nameRow.AddChild(_newFormatNameField);
        vbox.AddChild(nameRow);

        var nameNote = new Label
        {
            Text = "Use snake_case, e.g., hedge4, wall_south",
            Modulate = new Color(0.7f, 0.7f, 0.7f)
        };
        nameNote.AddThemeFontSizeOverride("font_size", 10);
        vbox.AddChild(nameNote);

        var typeRow = CreateRow("Bitmask Type:");
        _newFormatTypeDropdown = new OptionButton
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _newFormatTypeDropdown.AddItem("Corner4 (16 variants)", 0);
        _newFormatTypeDropdown.AddItem("Edge4 (16 variants)", 1);
        _newFormatTypeDropdown.AddItem("Full8 (47 variants)", 2);
        typeRow.AddChild(_newFormatTypeDropdown);
        vbox.AddChild(typeRow);

        _newFormatDialog.AddChild(vbox);
        AddChild(_newFormatDialog);
        _newFormatDialog.Confirmed += OnNewFormatConfirmed;
    }

    private void OnDeletePressed()
    {
        if (_selectedFormat == null || _selectedFormat.IsBuiltIn)
            return;

        var dialog = new ConfirmationDialog
        {
            DialogText = $"Delete format '{_selectedFormat.Name}'?\n\nThis cannot be undone.",
            OkButtonText = "Delete"
        };
        dialog.Confirmed += () => DeleteSelectedFormat(dialog);
        dialog.Canceled += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private void DeleteSelectedFormat(ConfirmationDialog dialog)
    {
        var name = _selectedFormat!.Name;
        if (AutoTileFormatRegistry.Unregister(name))
        {
            _customFormats.Remove(name);
            _selectedFormat = null;
            _selectedFormatName = null;
            PopulateFormatList();
            ShowNoSelection();
            EmitSignal(SignalName.FormatDeleted, name);
        }

        dialog.QueueFree();
    }

    private void ShowError(string message)
    {
        var dialog = new AcceptDialog
        {
            Title = "Error",
            DialogText = message
        };
        dialog.Confirmed += () => dialog.QueueFree();
        AddChild(dialog);
        dialog.PopupCentered();
    }

    private static Dictionary<int, VariantDefinition> CreateDefaultVariantMappings(
        BitmaskType type,
        HashSet<int> allowedBitmasks)
    {
        var mappings = new Dictionary<int, VariantDefinition>();
        foreach (var bitmask in allowedBitmasks)
            mappings[bitmask] = new VariantDefinition(Vector2I.Zero);
        return mappings;
    }

    private static HBoxContainer CreateRow(string label)
    {
        var row = new HBoxContainer();
        var labelControl = new Label
        {
            Text = label,
            CustomMinimumSize = new Vector2(100, 0)
        };
        row.AddChild(labelControl);
        return row;
    }
}
#endif
