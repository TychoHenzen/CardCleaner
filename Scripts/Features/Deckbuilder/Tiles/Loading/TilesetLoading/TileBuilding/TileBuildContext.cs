using System;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.TilesetLoading.WangSets;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.TilesetLoading.TileBuilding;

/// <summary>
/// Shared inputs for building tile definitions from one parsed tileset.
/// </summary>
internal sealed class TileBuildContext
{
    internal required WangSetData WangData { get; init; }
    internal required int Columns { get; init; }
    internal required int SourceId { get; init; }
    internal required Func<string, TileShape> ShapeOf { get; init; }
}
