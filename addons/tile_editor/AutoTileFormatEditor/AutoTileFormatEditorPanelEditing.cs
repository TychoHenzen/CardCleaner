#if TOOLS
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    private void OnNameChanged(string newName)
    {
        if (_isUpdating || _selectedFormat == null || _selectedFormat.IsBuiltIn)
            return;
    }

    private void OnBitmaskTypeChanged(long index)
    {
        if (_isUpdating || _selectedFormat == null || _selectedFormat.IsBuiltIn)
            return;

        var newType = index switch
        {
            1 => BitmaskType.Edge4,
            2 => BitmaskType.Full8,
            _ => BitmaskType.Corner4
        };
        UpdateCustomFormat(_selectedFormat.Name, newType, null);
    }

    private void OnAllowedBitmasksChanged()
    {
        if (_isUpdating || _selectedFormat == null || _selectedFormat.IsBuiltIn)
            return;
        if (_bitmaskGrid == null)
            return;

        UpdateCustomFormat(
            _selectedFormat.Name,
            null,
            _bitmaskGrid.AllowedBitmasks.ToHashSet());
        _variantCountLabel!.Text = _bitmaskGrid.AllowedBitmasks.Count.ToString();
        RebuildVariantConfigRows();
    }

    private void UpdateCustomFormat(
        string name,
        BitmaskType? newType,
        HashSet<int>? newAllowedBitmasks)
    {
        if (!_customFormats.TryGetValue(name, out var editableFormat))
            return;

        if (newType.HasValue)
        {
            editableFormat.BitmaskType = newType.Value;
            editableFormat.AllowedBitmasks = AutoTileFormatDefinition.AllBitmasksFor(newType.Value);
            _bitmaskGrid?.Configure(newType.Value, editableFormat.AllowedBitmasks, false);
        }

        if (newAllowedBitmasks != null)
            editableFormat.AllowedBitmasks = newAllowedBitmasks;

        ReregisterCustomFormat(editableFormat);
        EmitSignal(SignalName.FormatModified, name);
    }

    private void ReregisterCustomFormat(EditableAutoTileFormat editableFormat)
    {
        AutoTileFormatRegistry.Unregister(editableFormat.Name);
        var variantMappings = ConvertToVariantMappings(editableFormat);
        var newDefinition = new AutoTileFormatDefinition(
            editableFormat.Name,
            editableFormat.BitmaskType,
            editableFormat.AllowedBitmasks,
            variantMappings,
            isBuiltIn: false);
        AutoTileFormatRegistry.Register(newDefinition);
        if (_selectedFormatName == editableFormat.Name)
            AutoTileFormatRegistry.TryGet(editableFormat.Name, out _selectedFormat);
    }

    private static Dictionary<int, VariantDefinition> ConvertToVariantMappings(
        EditableAutoTileFormat editableFormat)
    {
        var mappings = new Dictionary<int, VariantDefinition>();
        foreach (var bitmask in editableFormat.AllowedBitmasks)
        {
            if (editableFormat.VariantMappings.TryGetValue(bitmask, out var editable))
            {
                mappings[bitmask] = new VariantDefinition(
                    Vector2I.Zero,
                    new Vector2I(editable.SizeX, editable.SizeY),
                    new Vector2I(editable.OffsetX, editable.OffsetY));
            }
            else
            {
                mappings[bitmask] = new VariantDefinition(Vector2I.Zero);
            }
        }

        return mappings;
    }
}
#endif
