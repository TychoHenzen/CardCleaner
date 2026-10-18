using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

/// <summary>
/// Picks the render coordinates for one dual-grid overlay cell from its base and top tiles and its bitmask.
/// </summary>
internal interface ITerrainTransitionResolver
{
    /// <summary>True when transitions come from the compiled atlas, false when the fallback path renders.</summary>
    bool UsingCompiledAtlas { get; }

    TileRenderCoordinates Resolve(
        TileDefinition baseTile,
        TileDefinition? topTile,
        string baseTileId,
        string topTileId,
        int bitmask,
        ref int transitionsResolved);
}
