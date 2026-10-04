#if TOOLS
using System.Collections.Generic;
using CardCleaner.Features.Worldgen.AutoTiling;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Mutable format data for custom formats being edited.
/// </summary>
public class EditableAutoTileFormat
{
    public string Name { get; set; } = "";
    public BitmaskType BitmaskType { get; set; } = BitmaskType.Corner4;
    public HashSet<int> AllowedBitmasks { get; set; } = new();
    public Dictionary<int, EditableFormatVariant> VariantMappings { get; set; } = new();
}
#endif
