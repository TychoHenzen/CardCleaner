using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.VariantModifiers;

/// <summary>
/// Selects tile variants using weighted random selection with a modifier pipeline.
/// All variants start with equal weight (1.0), then modifiers adjust weights based on context.
/// </summary>
public sealed class WeightedVariantSelector
{
    private readonly VariantWeightPipeline _pipeline;

    public WeightedVariantSelector(VariantWeightPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        _pipeline = pipeline;
    }

    /// <summary>
    /// Select a variant index for the given tile at the specified position.
    /// </summary>
    /// <param name="position">Position being evaluated.</param>
    /// <param name="placedTiles">Already placed tiles on the map.</param>
    /// <param name="biome">Biome at the position.</param>
    /// <param name="tile">The tile definition to select variant for.</param>
    /// <param name="rng">Random number generator.</param>
    /// <returns>Selected variant index (0-based), or -1 if tile has no variants.</returns>
    public int SelectVariant(
        Vector2I position,
        IReadOnlyDictionary<Vector2I, string> placedTiles,
        BiomeDefinition biome,
        TileDefinition tile,
        RandomNumberGenerator rng)
    {
        // Check if tile has variants
        if (!tile.HasVariations || tile.Variations == null)
            return -1;

        var variantCount = tile.Variations.Length;
        if (variantCount == 0)
            return -1;

        // If only one variant, no selection needed
        if (variantCount == 1)
            return 0;

        // Initialize weights - all variants start at 1.0
        var weights = new float[variantCount];
        for (var i = 0; i < variantCount; i++)
        {
            weights[i] = 1.0f;
        }

        // Create context and apply modifiers
        var context = new VariantSelectionContext(position, placedTiles, biome, tile, rng, weights);
        _pipeline.ApplyAll(context);

        // Perform weighted random selection
        return SelectWeighted(weights, rng);
    }

    /// <summary>
    /// Select a variant, returning the atlas coordinates for that variant.
    /// </summary>
    /// <param name="position">Position being evaluated.</param>
    /// <param name="placedTiles">Already placed tiles on the map.</param>
    /// <param name="biome">Biome at the position.</param>
    /// <param name="tile">The tile definition to select variant for.</param>
    /// <param name="rng">Random number generator.</param>
    /// <returns>Atlas coordinates for selected variant, or base tile coords if no variants.</returns>
    public Vector2I SelectVariantCoords(
        Vector2I position,
        IReadOnlyDictionary<Vector2I, string> placedTiles,
        BiomeDefinition biome,
        TileDefinition tile,
        RandomNumberGenerator rng)
    {
        var variantIndex = SelectVariant(position, placedTiles, biome, tile, rng);

        if (variantIndex < 0 || tile.Variations == null || variantIndex >= tile.Variations.Length)
            return tile.AtlasCoords;

        return tile.Variations[variantIndex];
    }

    /// <summary>
    /// Perform weighted random selection from the weights array.
    /// </summary>
    private static int SelectWeighted(float[] weights, RandomNumberGenerator rng)
    {
        // Calculate total weight (only positive weights count)
        var totalWeight = 0f;
        foreach (var weight in weights)
        {
            if (weight > 0)
                totalWeight += weight;
        }

        if (totalWeight <= 0)
        {
            // All weights zero or negative - fall back to random uniform selection
            return rng.RandiRange(0, weights.Length - 1);
        }

        // Roll and select
        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;

        for (var i = 0; i < weights.Length; i++)
        {
            if (weights[i] <= 0)
                continue;

            cumulative += weights[i];
            if (roll <= cumulative)
                return i;
        }

        // Fallback: return first positive-weight variant
        for (var i = 0; i < weights.Length; i++)
        {
            if (weights[i] > 0)
                return i;
        }

        // Should never reach here, but return 0 as last resort
        return 0;
    }
}
