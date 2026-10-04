#if TOOLS
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    private void PopulateFormatList()
    {
        _formatList?.Clear();
        var formats = AutoTileFormatRegistry.GetAll()
            .OrderBy(format => format.IsBuiltIn ? 0 : 1)
            .ThenBy(format => format.Name);
        foreach (var format in formats)
        {
            var displayName = format.IsBuiltIn
                ? $"{format.Name} (built-in)"
                : format.Name;
            var index = _formatList!.AddItem(displayName);
            _formatList.SetItemMetadata(index, format.Name);
            if (format.IsBuiltIn)
                _formatList.SetItemCustomFgColor(index, new Color(0.7f, 0.7f, 0.7f));
        }
    }

    private void OnFormatSelected(long index)
    {
        var formatName = _formatList!.GetItemMetadata((int)index).AsString();
        SelectFormat(formatName);
    }

    private void SelectFormat(string formatName)
    {
        _selectedFormatName = formatName;
        if (!AutoTileFormatRegistry.TryGet(formatName, out var format) || format == null)
        {
            _selectedFormat = null;
            ShowNoSelection();
            return;
        }

        _selectedFormat = format;
        ShowFormatDetails();
    }

    private void ShowNoSelection()
    {
        _noSelectionLabel!.Visible = true;
        _formatDetailsContainer!.Visible = false;
        _deleteButton!.Disabled = true;
    }

    private void ShowFormatDetails()
    {
        if (_selectedFormat == null)
            return;

        _isUpdating = true;
        _noSelectionLabel!.Visible = false;
        _formatDetailsContainer!.Visible = true;
        _nameField!.Text = _selectedFormat.Name;
        _nameField.Editable = !_selectedFormat.IsBuiltIn;
        _bitmaskTypeDropdown!.Selected = _selectedFormat.BitmaskType switch
        {
            BitmaskType.Corner4 => 0,
            BitmaskType.Edge4 => 1,
            BitmaskType.Full8 => 2,
            _ => 0
        };
        _bitmaskTypeDropdown.Disabled = _selectedFormat.IsBuiltIn;
        _variantCountLabel!.Text = _selectedFormat.AllowedBitmasks.Count.ToString();
        UpdateBuiltInNote();
        _bitmaskGrid!.Configure(
            _selectedFormat.BitmaskType,
            _selectedFormat.AllowedBitmasks,
            _selectedFormat.IsBuiltIn);
        _deleteButton!.Disabled = _selectedFormat.IsBuiltIn;
        RebuildVariantConfigRows();
        if (_variantConfigFoldout != null)
            _variantConfigFoldout.Visible = !_selectedFormat.IsBuiltIn;
        _isUpdating = false;
    }

    private void UpdateBuiltInNote()
    {
        var builtInNote = _formatDetailsContainer!.GetNode<Label>("BuiltInNote");
        if (builtInNote == null)
            return;

        builtInNote.Text = _selectedFormat!.IsBuiltIn
            ? "(Built-in format - cannot be modified)"
            : "(Custom format - editable)";
        builtInNote.Modulate = _selectedFormat.IsBuiltIn
            ? new Color(1f, 0.8f, 0.4f)
            : new Color(0.4f, 0.8f, 0.4f);
    }

    private void RebuildVariantConfigRows()
    {
        if (_variantConfigContainer == null || _selectedFormat == null)
            return;

        foreach (var child in _variantConfigContainer.GetChildren())
            child.QueueFree();
        _variantConfigRows.Clear();

        EditableAutoTileFormat? editableFormat = null;
        if (!_selectedFormat.IsBuiltIn && _selectedFormatName != null)
            _customFormats.TryGetValue(_selectedFormatName, out editableFormat);

        var sortedBitmasks = _selectedFormat.AllowedBitmasks.OrderBy(bitmask => bitmask);
        foreach (var bitmask in sortedBitmasks)
        {
            var row = new VariantConfigRow(
                bitmask,
                _selectedFormat.BitmaskType,
                editableFormat,
                _selectedFormat.IsBuiltIn);
            row.VariantChanged += OnVariantConfigChanged;
            _variantConfigContainer.AddChild(row);
            _variantConfigRows[bitmask] = row;
        }
    }

    private void OnVariantConfigChanged(int bitmask)
    {
        if (_isUpdating || _selectedFormat == null || _selectedFormat.IsBuiltIn)
            return;
        if (_selectedFormatName == null)
            return;
        if (!_customFormats.TryGetValue(_selectedFormatName, out var editableFormat))
            return;

        if (_variantConfigRows.TryGetValue(bitmask, out var row))
            editableFormat.VariantMappings[bitmask] = row.GetVariant();
        ReregisterCustomFormat(editableFormat);
        EmitSignal(SignalName.FormatModified, _selectedFormatName);
    }
}
#endif
