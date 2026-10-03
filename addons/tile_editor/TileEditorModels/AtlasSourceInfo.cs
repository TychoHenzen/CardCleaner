#if TOOLS
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Information about a TileSetAtlasSource for UI display
/// </summary>
public class AtlasSourceInfo
{
    public int SourceId { get; set; }
    public string DisplayName { get; set; } = "";
    public TileSetAtlasSource? Source { get; set; }
}
#endif
