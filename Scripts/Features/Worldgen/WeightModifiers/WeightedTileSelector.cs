using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Selects tiles using weighted random selection with a modifier pipeline.
/// All candidate tiles start with equal weight (1.0), then modifiers adjust weights.
/// </summary>
public sealed class WeightedTileSelector
{
    private readonly ITileRegistry _tileRegistry;
    private readonly WeightModifierPipeline _pipeline;

    public WeightedTileSelector(ITileRegistry tileRegistry, WeightModifierPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(tileRegistry);
        ArgumentNullException.ThrowIfNull(pipeline);

        _tileRegistry = tileRegistry;
        _pipeline = pipeline;
    }

    /// <summary>
    /// Select a tile for the given position using weighted random selection.
    /// </summary>
    /// <param name="position">Position being evaluated.</param>
    /// <param name="placedTiles">Already placed tiles on the map.</param>
    /// <param name="biome">Biome at the position.</param>
    /// <param name="rng">Random number generator.</param>
    /// <param name="candidateTileIds">Specific tiles to consider (null = all tiles).</param>
    /// <returns>Selected tile ID, or null if no valid tiles.</returns>
    public string? SelectTile(
        Vector2I position,
        IReadOnlyDictionary<Vector2I, string> placedTiles,
        BiomeDefinition biome,
        RandomNumberGenerator rng,
        IEnumerable<string>? candidateTileIds = null)
    {
        // Initialize weights - all candidates start at 1.0
        var weights = InitializeWeights(candidateTileIds);

        if (weights.Count == 0)
            return null;

        // Create context and apply modifiers
        var context = new TileSelectionContext(position, placedTiles, biome, rng, weights);
        _pipeline.ApplyAll(context);

        // Perform weighted random selection
        return SelectWeighted(weights, rng);
    }

    /// <summary>
    /// Initialize weights dictionary with all candidates at 1.0.
    /// </summary>
    private Dictionary<string, float> InitializeWeights(IEnumerable<string>? candidateTileIds)
    {
        var weights = new Dictionary<string, float>();

        if (candidateTileIds != null)
        {
            foreach (var tileId in candidateTileIds)
            {
                weights[tileId] = 1.0f;
            }
        }
        else
        {
            foreach (var tile in _tileRegistry.GetAllTiles())
            {
                weights[tile.Id] = 1.0f;
            }
        }

        return weights;
    }

    /// <summary>
    /// Perform weighted random selection from the weights dictionary.
    /// Tiles with zero or negative weight are excluded.
    /// </summary>
    private static string? SelectWeighted(Dictionary<string, float> weights, RandomNumberGenerator rng)
    {
        // Calculate total weight (only positive weights count)
        var totalWeight = 0f;
        foreach (var weight in weights.Values)
        {
            if (weight > 0)
                totalWeight += weight;
        }

        if (totalWeight <= 0)
            return null;

        // Roll and select
        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;

        foreach (var (tileId, weight) in weights)
        {
            if (weight <= 0)
                continue;

            cumulative += weight;
            if (roll <= cumulative)
                return tileId;
        }

        // Fallback: return first positive-weight tile
        foreach (var (tileId, weight) in weights)
        {
            if (weight > 0)
                return tileId;
        }

        return null;
    }
}
