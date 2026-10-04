#if TOOLS
namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Outcome of inserting missing properties into every TSX file of a directory.
/// </summary>
internal readonly record struct TsxDirectoryInsertResult(
    bool Success,
    string Message,
    int FilesModified,
    int TotalTiles,
    int TotalWangSets);
#endif
