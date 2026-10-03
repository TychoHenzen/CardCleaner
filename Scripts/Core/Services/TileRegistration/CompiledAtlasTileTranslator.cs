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
            original.Id,
            original.Name,
            original.Passability,
            newCoords,
            new TileDefinitionOptions
            {
                SourceId = newSourceId,
                Layer = original.Layer,
                Elevation = original.Elevation,
                IsTransparent = original.IsTransparent,
                AllowedBiomes = original.AllowedBiomes,
                Size = original.Size,
                DecorationDensity = original.DecorationDensity,
                AutoTileVariants = TranslateAutoTileVariants(original),
                AutoTileFormatName = original.AutoTileFormatName,
                Variations = TranslateVariations(original),
                VariationMode = original.VariationMode,
                Animation = TranslateAnimation(original),
                Dominance = original.Dominance,
                InnerTerrainId = original.InnerTerrainId,
                OuterTerrainId = original.OuterTerrainId,
                IsGapTile = original.IsGapTile,
                Probability = original.Probability
            });
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
