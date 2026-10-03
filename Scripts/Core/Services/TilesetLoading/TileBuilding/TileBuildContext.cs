namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;

/// <summary>
/// Shared inputs for building tile definitions from one parsed tileset.
/// </summary>
internal sealed class TileBuildContext
{
    internal required WangSetData WangData { get; init; }
    internal required int Columns { get; init; }
    internal required int SourceId { get; init; }
}
