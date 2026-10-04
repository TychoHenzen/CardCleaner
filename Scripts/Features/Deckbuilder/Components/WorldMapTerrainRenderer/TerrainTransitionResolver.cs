using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

internal sealed class TerrainTransitionResolver
{
    private readonly bool _usingCompiledAtlas;
    private readonly ITransitionResolver? _transitionResolver;

    public TerrainTransitionResolver(bool usingCompiledAtlas)
    {
        _usingCompiledAtlas = usingCompiledAtlas;
        if (usingCompiledAtlas)
            _transitionResolver = new CompiledTransitionResolver();
    }

    public bool UsingCompiledAtlas => _usingCompiledAtlas;

    public TileRenderCoordinates Resolve(
        TileDefinition baseTile,
        TileDefinition? topTile,
        string baseTileId,
        string topTileId,
        int bitmask,
        ref int transitionsResolved)
    {
        if (!_usingCompiledAtlas || _transitionResolver == null)
            return ResolveWithoutCompiledAtlas(baseTile, topTile, bitmask);

        if (bitmask == 0)
            return ResolveEmptyBitmask(baseTile, baseTileId);

        if (topTileId == baseTileId && bitmask == 15)
            return ResolveSolidFill(baseTile, topTileId, ref transitionsResolved);

        return ResolveTransition(
            baseTile,
            topTile,
            topTileId,
            baseTileId,
            bitmask,
            ref transitionsResolved);
    }

    private static TileRenderCoordinates ResolveWithoutCompiledAtlas(
        TileDefinition baseTile,
        TileDefinition? topTile,
        int bitmask)
    {
        var effectiveTile = topTile ?? baseTile;
        var atlasCoords = effectiveTile.HasAutoTileVariants
            ? effectiveTile.GetAutoTileCoords(bitmask)
            : effectiveTile.AtlasCoords;
        return new TileRenderCoordinates(effectiveTile.SourceId, atlasCoords);
    }

    private TileRenderCoordinates ResolveEmptyBitmask(TileDefinition baseTile, string baseTileId)
    {
        var resolver = _transitionResolver!;
        var solidFillCoords = resolver.ResolveSolidFill(baseTileId);
        if (solidFillCoords.HasValue)
            return new TileRenderCoordinates(resolver.CompiledAtlasSourceId, solidFillCoords.Value);

        var translated = CompiledAtlasLoader.TranslateCoordinates(
            baseTile.SourceId,
            baseTile.AtlasCoords);
        return new TileRenderCoordinates(translated.SourceId, translated.AtlasCoords);
    }

    private TileRenderCoordinates ResolveSolidFill(
        TileDefinition baseTile,
        string topTileId,
        ref int transitionsResolved)
    {
        var resolver = _transitionResolver!;
        var solidFillCoords = resolver.ResolveSolidFill(topTileId);
        if (solidFillCoords.HasValue)
        {
            transitionsResolved++;
            return new TileRenderCoordinates(resolver.CompiledAtlasSourceId, solidFillCoords.Value);
        }

        if (baseTile.SourceId == resolver.CompiledAtlasSourceId)
        {
            var atlasCoords = baseTile.HasAutoTileVariants
                ? baseTile.GetAutoTileCoords(15)
                : baseTile.AtlasCoords;
            return new TileRenderCoordinates(baseTile.SourceId, atlasCoords);
        }

        return new TileRenderCoordinates(resolver.CompiledAtlasSourceId, Vector2I.Zero);
    }

    private TileRenderCoordinates ResolveTransition(
        TileDefinition baseTile,
        TileDefinition? topTile,
        string topTileId,
        string baseTileId,
        int bitmask,
        ref int transitionsResolved)
    {
        var resolver = _transitionResolver!;
        var effectiveTopTile = topTile ?? baseTile;
        var fallbackCoords = effectiveTopTile.HasAutoTileVariants
            ? effectiveTopTile.GetAutoTileCoords(bitmask)
            : effectiveTopTile.AtlasCoords;
        var result = resolver.ResolveWithFallback(
            topTileId,
            baseTileId,
            bitmask,
            effectiveTopTile.SourceId,
            fallbackCoords);

        if (result.SourceId == resolver.CompiledAtlasSourceId)
            transitionsResolved++;
        return new TileRenderCoordinates(result.SourceId, result.AtlasCoords);
    }
}
