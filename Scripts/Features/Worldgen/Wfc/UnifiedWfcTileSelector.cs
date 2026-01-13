using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Unified tile selector that works with any IWfcGrid implementation.
/// Selects tiles from valid options using weighted probabilities.
/// Supports constraints via the IUnifiedWfcConstraint interface.
/// </summary>
public class UnifiedWfcTileSelector
{
    /// <summary>
    /// Penalty multiplier for tiles not in the biome's preferred set.
    /// </summary>
    public float NonBiomeTilePenalty { get; set; } = 0.1f;

    /// <summary>
    /// Default weight for tiles not explicitly defined in biome pools.
    /// </summary>
    public float DefaultTileWeight { get; set; } = 1.0f;

    /// <summary>
    /// Weight multiplier for tiles matching collapsed neighbors.
    /// </summary>
    public float ContinuityBiasMultiplier { get; set; } = 2.0f;

    /// <summary>
    /// Selects a tile from the valid options using weighted probabilities.
    /// </summary>
    /// <param name="validTiles">Tiles that satisfy hard constraints</param>
    /// <param name="biome">Current biome for soft rule weights</param>
    /// <param name="rng">Random number generator</param>
    /// <param name="continuityTiles">Tiles that match collapsed neighbors</param>
    /// <param name="cellId">Cell ID for constraint context</param>
    /// <param name="grid">WFC grid for constraint context</param>
    /// <param name="constraints">Constraints to apply</param>
    /// <returns>Selected tile ID, or null if no valid tiles</returns>
    public string? SelectTile(
        IReadOnlyCollection<string> validTiles,
        BiomeDefinition? biome,
        RandomNumberGenerator rng,
        IReadOnlySet<string>? continuityTiles,
        int cellId,
        IWfcGrid grid,
        IEnumerable<IUnifiedWfcConstraint>? constraints = null)
    {
        if (validTiles.Count == 0)
            return null;

        // Fast path: single tile
        if (validTiles.Count == 1)
        {
            foreach (var tile in validTiles)
                return tile;
        }

        var weights = ComputeWeights(validTiles, biome, rng, continuityTiles, cellId, grid, constraints);

        // Calculate total weight
        var totalWeight = 0f;
        foreach (var weight in weights.Values)
        {
            totalWeight += weight;
        }

        // Edge case: all weights are zero
        if (totalWeight <= 0)
            return null;

        // Weighted random selection
        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;

        foreach (var (tileId, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return tileId;
        }

        // Fallback
        return validTiles.Last();
    }

    /// <summary>
    /// Computes weights for all valid tiles at a cell.
    /// Used for weighted entropy calculation during cell selection.
    /// </summary>
    public IReadOnlyDictionary<string, float> ComputeWeights(
        IReadOnlyCollection<string> validTiles,
        BiomeDefinition? biome,
        RandomNumberGenerator rng,
        IReadOnlySet<string>? continuityTiles,
        int cellId,
        IWfcGrid grid,
        IEnumerable<IUnifiedWfcConstraint>? constraints = null)
    {
        var weights = new Dictionary<string, float>(validTiles.Count);

        if (validTiles.Count == 0)
            return weights;

        var biomeWeights = BuildBiomeWeightLookup(biome);

        foreach (var tileId in validTiles)
        {
            float weight;

            // Get base weight from biome or default
            if (biomeWeights.TryGetValue(tileId, out var biomeWeight))
            {
                weight = biomeWeight;
            }
            else
            {
                weight = DefaultTileWeight * NonBiomeTilePenalty;
            }

            // Apply continuity bias if tile matches a collapsed neighbor
            if (continuityTiles != null && continuityTiles.Contains(tileId))
            {
                weight *= ContinuityBiasMultiplier;
            }

            // Apply constraint modifiers
            if (constraints != null)
            {
                foreach (var constraint in constraints)
                {
                    var modifier = constraint.GetWeightModifier(cellId, tileId, grid);
                    if (modifier == 0f)
                    {
                        weight = 0f;
                        break;
                    }
                    weight *= modifier;
                }
            }

            weights[tileId] = weight;
        }

        return weights;
    }

    /// <summary>
    /// Selects a tile with uniform probability (no biome weighting).
    /// </summary>
    public string? SelectUniform(IReadOnlyCollection<string> validTiles, RandomNumberGenerator rng)
    {
        if (validTiles.Count == 0)
            return null;

        var index = rng.RandiRange(0, validTiles.Count - 1);

        if (validTiles is IList<string> list)
            return list[index];

        var i = 0;
        foreach (var tile in validTiles)
        {
            if (i == index)
                return tile;
            i++;
        }

        return null;
    }

    private Dictionary<string, float> BuildBiomeWeightLookup(BiomeDefinition? biome)
    {
        var lookup = new Dictionary<string, float>();

        if (biome?.PassableTiles == null)
            return lookup;

        foreach (var entry in biome.PassableTiles.Entries)
        {
            if (!string.IsNullOrEmpty(entry.TileId))
            {
                lookup[entry.TileId] = entry.Weight;
            }
        }

        return lookup;
    }
}
