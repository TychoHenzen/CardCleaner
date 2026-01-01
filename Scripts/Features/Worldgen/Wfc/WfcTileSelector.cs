using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Selects tiles from valid options using weighted probabilities.
/// Applies soft rules: biome-preferred tiles get full weight, others are penalized.
/// Supports continuity bias to encourage larger contiguous regions.
/// Supports pluggable constraints for extensible weight adjustments.
/// </summary>
public class WfcTileSelector
{
    private readonly List<IWfcConstraint> _constraints = new();

    /// <summary>
    /// Registers a constraint to be applied during tile selection.
    /// Constraints are applied multiplicatively: final_weight = base_weight × Π(modifiers)
    /// Return 0.0 to ban a tile, 1.0 for neutral, >1.0 for boost.
    /// </summary>
    public void AddConstraint(IWfcConstraint constraint) => _constraints.Add(constraint);

    /// <summary>
    /// Clears all registered constraints.
    /// </summary>
    public void ClearConstraints() => _constraints.Clear();

    /// <summary>
    /// Penalty multiplier for tiles not in the biome's preferred set.
    /// Default 0.1 means non-biome tiles are 10x less likely to be selected.
    /// </summary>
    public float NonBiomeTilePenalty { get; set; } = 0.1f;

    /// <summary>
    /// Default weight for tiles not explicitly defined in biome pools.
    /// </summary>
    public float DefaultTileWeight { get; set; } = 1.0f;

    /// <summary>
    /// Weight multiplier for tiles matching collapsed neighbors.
    /// Default 5.0 means matching tiles are 5x more likely to be selected.
    /// Set to 1.0 to disable continuity bias.
    /// </summary>
    public float ContinuityBiasMultiplier { get; set; } = 5.0f;

    /// <summary>
    /// Selects a tile from the valid options using biome-weighted probabilities.
    /// </summary>
    /// <param name="validTiles">Tiles that satisfy hard constraints (from WfcCellState)</param>
    /// <param name="biome">Current biome for soft rule weights</param>
    /// <param name="rng">Random number generator</param>
    /// <param name="continuityTiles">Optional set of tiles that match collapsed neighbors (for continuity bias)</param>
    /// <param name="position">Grid position for soft modifier context</param>
    /// <param name="grid">WFC grid for soft modifier context</param>
    /// <returns>Selected tile ID, or null if no valid tiles</returns>
    public string? SelectTile(
        IReadOnlyCollection<string> validTiles,
        BiomeDefinition? biome,
        RandomNumberGenerator rng,
        IReadOnlySet<string>? continuityTiles = null,
        Vector2I? position = null,
        WfcGrid? grid = null)
    {
        if (validTiles.Count == 0)
            return null;

        if (validTiles.Count == 1)
            return validTiles.First();

        // Build weight lookup from biome
        var biomeWeights = BuildBiomeWeightLookup(biome);

        // Calculate weighted probabilities
        var weights = new List<(string tileId, float weight)>();
        var totalWeight = 0f;

        foreach (var tileId in validTiles)
        {
            float weight;
            if (biomeWeights.TryGetValue(tileId, out var biomeWeight))
            {
                // Tile is in biome's preferred set - use its defined weight
                weight = biomeWeight;
            }
            else
            {
                // Tile is not in biome - apply penalty
                weight = DefaultTileWeight * NonBiomeTilePenalty;
            }

            // Apply continuity bias if tile matches a collapsed neighbor
            if (continuityTiles != null && continuityTiles.Contains(tileId))
            {
                weight *= ContinuityBiasMultiplier;
            }

            // Apply registered constraints
            if (position.HasValue && grid != null && _constraints.Count > 0)
            {
                var constraintContext = new WfcConstraintContext
                {
                    Position = position.Value,
                    TileId = tileId,
                    Grid = grid,
                    Rng = rng
                };

                foreach (var constraint in _constraints)
                {
                    var modifier = constraint.GetProbabilityModifier(constraintContext);
                    if (modifier == 0f)
                    {
                        weight = 0f;
                        break;
                    }
                    weight *= modifier;
                }
            }

            weights.Add((tileId, weight));
            totalWeight += weight;
        }

        // Edge case: all weights are zero
        if (totalWeight <= 0)
            return validTiles.First();

        // Weighted random selection
        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;

        foreach (var (tileId, weight) in weights)
        {
            cumulative += weight;
            if (roll <= cumulative)
                return tileId;
        }

        // Fallback (shouldn't reach here)
        return weights[^1].tileId;
    }

    /// <summary>
    /// Selects a tile with uniform probability (no biome weighting).
    /// Useful for testing or biome-agnostic selection.
    /// </summary>
    public string? SelectUniform(IReadOnlyCollection<string> validTiles, RandomNumberGenerator rng)
    {
        if (validTiles.Count == 0)
            return null;

        var index = rng.RandiRange(0, validTiles.Count - 1);
        return validTiles.ElementAt(index);
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
