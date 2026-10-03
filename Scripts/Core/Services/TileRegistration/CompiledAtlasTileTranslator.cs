using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Rewrites a tile definition so its atlas coordinates point into the compiled atlas.
/// </summary>
internal sealed class CompiledAtlasTileTranslator
{
    private readonly CompiledAtlasLoader.AtlasMappingData _mapping;

    internal CompiledAtlasTileTranslator(CompiledAtlasLoader.AtlasMappingData mapping)
    {
        _mapping = mapping;
    }

    internal TileDefinition Translate(TileDefinition original)
    {
        // Translate base coordinates
        var (newSourceId, newCoords) = CompiledAtlasLoader.TranslateCoordinates(
            original.SourceId,
            original.AtlasCoords,
            _mapping);

        // Create new tile definition with translated coordinates
        return new TileDefinition(
            id: original.Id,
            name: original.Name,
            passability: original.Passability,
            atlasCoords: newCoords,
            sourceId: newSourceId,
            layer: original.Layer,
            elevation: original.Elevation,
            isTransparent: original.IsTransparent,
            allowedBiomes: original.AllowedBiomes,
            size: original.Size,
            decorationDensity: original.DecorationDensity,
            autoTileVariants: TranslateAutoTileVariants(original),
            autoTileFormatName: original.AutoTileFormatName,
            variations: TranslateVariations(original),
            variationMode: original.VariationMode,
            animation: TranslateAnimation(original),
            dominance: original.Dominance,
            innerTerrainId: original.InnerTerrainId,
            outerTerrainId: original.OuterTerrainId,
            isGapTile: original.IsGapTile,
            probability: original.Probability);
    }

    private Vector2I TranslateCoords(int sourceId, Vector2I coords)
    {
        var (_, translated) = CompiledAtlasLoader.TranslateCoordinates(sourceId, coords, _mapping);
        return translated;
    }

    private Vector2I?[]? TranslateAutoTileVariants(TileDefinition original)
    {
        if (original.AutoTileVariants == null)
            return null;

        var translatedVariants = new Vector2I?[original.AutoTileVariants.Length];
        for (var i = 0; i < original.AutoTileVariants.Length; i++)
        {
            var variant = original.AutoTileVariants[i];
            if (variant.HasValue)
                translatedVariants[i] = TranslateCoords(original.SourceId, variant.Value);
        }

        return translatedVariants;
    }

    private Vector2I[]? TranslateVariations(TileDefinition original)
    {
        if (original.Variations == null)
            return null;

        var translatedVars = new Vector2I[original.Variations.Length];
        for (var i = 0; i < original.Variations.Length; i++)
            translatedVars[i] = TranslateCoords(original.SourceId, original.Variations[i]);

        return translatedVars;
    }

    private TileAnimation? TranslateAnimation(TileDefinition original)
    {
        if (original.Animation == null)
            return null;

        var translatedFrames = new Vector2I[original.Animation.Frames.Length];
        for (var i = 0; i < original.Animation.Frames.Length; i++)
            translatedFrames[i] = TranslateCoords(original.SourceId, original.Animation.Frames[i]);

        return new TileAnimation(translatedFrames, original.Animation.FrameDuration);
    }
}
