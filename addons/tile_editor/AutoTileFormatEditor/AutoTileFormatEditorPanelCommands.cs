#if TOOLS
using System.Text.RegularExpressions;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    private void OnNewFormatConfirmed()
    {
        var name = _newFormatNameField!.Text.Trim().ToLowerInvariant();
        if (!ValidateNewFormatName(name))
            return;

        var bitmaskType = GetNewFormatBitmaskType();
        var allowedBitmasks = AutoTileFormatDefinition.AllBitmasksFor(bitmaskType);
        var variantMappings = CreateDefaultVariantMappings(bitmaskType, allowedBitmasks);
        var newFormat = new AutoTileFormatDefinition(
            name,
            bitmaskType,
            allowedBitmasks,
            variantMappings,
            isBuiltIn: false);
        if (AutoTileFormatRegistry.Register(newFormat))
            RegisterNewFormat(name, bitmaskType, allowedBitmasks);
    }

    private bool ValidateNewFormatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Name cannot be empty");
            return false;
        }

        if (!Regex.IsMatch(name, @"^[a-z][a-z0-9_]*$"))
        {
            ShowError("Name must be snake_case (lowercase letters, numbers, underscores)");
            return false;
        }

        if (AutoTileFormatRegistry.Contains(name))
        {
            ShowError($"A format named '{name}' already exists");
            return false;
        }

        return true;
    }

    private BitmaskType GetNewFormatBitmaskType()
    {
        return _newFormatTypeDropdown!.Selected switch
        {
            1 => BitmaskType.Edge4,
            2 => BitmaskType.Full8,
            _ => BitmaskType.Corner4
        };
    }

    private void RegisterNewFormat(
        string name,
        BitmaskType bitmaskType,
        System.Collections.Generic.HashSet<int> allowedBitmasks)
    {
        _customFormats[name] = new EditableAutoTileFormat
        {
            Name = name,
            BitmaskType = bitmaskType,
            AllowedBitmasks = allowedBitmasks
        };

        PopulateFormatList();
        EmitSignal(SignalName.FormatCreated, name);
        SelectCreatedFormat(name);
    }

    private void SelectCreatedFormat(string name)
    {
        for (var index = 0; index < _formatList!.ItemCount; index++)
        {
            if (_formatList.GetItemMetadata(index).AsString() != name)
                continue;

            _formatList.Select(index);
            SelectFormat(name);
            break;
        }
    }
}
#endif
