using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

/// <summary>
/// Creates the terrain transition resolver the world map renderers use. A null resolver means the compiled atlas
/// is unavailable and the renderer falls back to the tiles' own auto-tile coordinates.
/// </summary>
internal static class TerrainTransitions
{
    public static ITerrainTransitionResolver Create(ITransitionResolver? resolver) =>
        new TerrainTransitionResolver(resolver);
}
