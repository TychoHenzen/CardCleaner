#if TOOLS
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    public override void _Ready()
    {
        if (_service == null)
            return;

        SizeFlagsVertical = SizeFlags.ExpandFill;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SplitOffset = 200;
        SetupLeftPanel();
        SetupRightPanel();
        _service.TilesLoaded += OnTilesLoaded;
        _service.AutoTileFormatsLoaded += OnAutoTileFormatsLoaded;
        CallDeferred(MethodName.PopulateFormatList);
        CallDeferred(MethodName.LoadCustomFormatsFromService);
    }

    private void OnTilesLoaded()
    {
        PopulateFormatList();
    }

    private void OnAutoTileFormatsLoaded()
    {
        LoadCustomFormatsFromService();
        PopulateFormatList();
    }

    private void LoadCustomFormatsFromService()
    {
        if (_service == null)
            return;

        _customFormats.Clear();
        foreach (var format in _service.CustomAutoTileFormats)
        {
            var editable = new EditableAutoTileFormat
            {
                Name = format.Name,
                BitmaskType = format.BitmaskType,
                AllowedBitmasks = new HashSet<int>(format.AllowedBitmasks)
            };
            foreach (var (bitmask, variant) in format.VariantMappings)
                editable.VariantMappings[bitmask] = variant.Clone();
            _customFormats[format.Name] = editable;
        }
    }
}
#endif
