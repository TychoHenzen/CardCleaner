#if TOOLS
namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Outcome of inserting missing properties into a single TSX file.
/// </summary>
internal readonly record struct TsxInsertResult(
    bool Success,
    string Message,
    int TilesModified,
    int WangSetsModified);
#endif
