#if TOOLS
using System.Collections.Generic;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Report of properties status in a TSX file.
/// </summary>
internal sealed class TsxPropertyReport
{
    internal string TsxPath { get; set; } = "";
    internal string FileName { get; set; } = "";
    internal int TilesWithId { get; set; }
    internal int WangSetCount { get; set; }
    internal HashSet<string> MissingTileProperties { get; set; } = new();
    internal HashSet<string> MissingWangSetProperties { get; set; } = new();

    internal bool HasMissingProperties =>
        MissingTileProperties.Count > 0 || MissingWangSetProperties.Count > 0;
}
#endif
