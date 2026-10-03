using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;

/// <summary>
/// Tile properties shared by Wang-set tiles and standalone property tiles.
/// </summary>
internal sealed class TileCommonProperties
{
    internal required TilePassability Passability { get; init; }
    internal required TileLayer Layer { get; init; }
    internal required float Elevation { get; init; }
    internal required bool IsTransparent { get; init; }
    internal required int Dominance { get; init; }
    internal required float DecorationDensity { get; init; }
    internal string? OuterTerrain { get; init; }
    internal string? InnerTerrain { get; init; }
}
