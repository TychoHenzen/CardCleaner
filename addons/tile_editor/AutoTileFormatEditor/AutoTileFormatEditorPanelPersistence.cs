#if TOOLS
using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Addons.TileEditor;

public partial class AutoTileFormatEditorPanel
{
    public IEnumerable<EditableAutoTileFormat> GetCustomFormats()
    {
        return _customFormats.Values;
    }

    public void LoadCustomFormats(IEnumerable<EditableAutoTileFormat> formats)
    {
        foreach (var format in formats)
        {
            if (AutoTileFormatRegistry.Contains(format.Name))
                continue;

            var variantMappings = CreateDefaultVariantMappings(
                format.BitmaskType,
                format.AllowedBitmasks);
            var definition = new AutoTileFormatDefinition(
                format.Name,
                format.BitmaskType,
                format.AllowedBitmasks,
                variantMappings,
                isBuiltIn: false);
            if (AutoTileFormatRegistry.Register(definition))
                _customFormats[format.Name] = format;
        }

        PopulateFormatList();
    }

    public void Refresh()
    {
        PopulateFormatList();
        if (_selectedFormatName != null)
            SelectFormat(_selectedFormatName);
    }
}
#endif
