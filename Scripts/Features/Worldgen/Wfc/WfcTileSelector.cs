using System;
using System.Collections.Generic;
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
    private bool _loggedConstraintCount;

    /// <summary>
    /// Registers a constraint to be applied during tile selection.
    /// Constraints are applied multiplicatively: final_weight = base_weight × Π(modifiers)
    /// Return 0.0 to ban a tile, 1.0 for neutral, >1.0 for boost.
    /// </summary>
    public void AddConstraint(IWfcConstraint constraint)
    {
        _constraints.Add(constraint);
        GD.Print($"[WfcTileSelector] Added constraint: {constraint.GetType().Name}, total: {_constraints.Count}");
    }

    /// <summary>
    /// Gets all registered constraints for entropy invalidation queries.
    /// </summary>
    public IEnumerable<IWfcConstraint> GetConstraints() => _constraints;

    /// <summary>
    /// Clears all registered constraints.
    /// </summary>
    public void ClearConstraints()
    {
        GD.Print($"[WfcTileSelector] ClearConstraints called, had {_constraints.Count} constraints");
        _constraints.Clear();
        _loggedConstraintCount = false;
    }

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
    /// Default 2.0 means matching tiles are 2x more likely to be selected.
    /// Reduced from 5.0 to allow biome affinity to dominate over neighbor continuity.
    /// Set to 1.0 to disable continuity bias.
    /// </summary>
    public float ContinuityBiasMultiplier { get; set; } = 2.0f;

    /// <summary>
    /// Selects a tile from the valid options using biome-weighted probabilities.
    /// Topology-agnostic version using cell IDs.
    /// </summary>
    /// <param name="validTiles">Tiles that satisfy hard constraints (from WfcCellState)</param>
    /// <param name="biome">Current biome for soft rule weights</param>
    /// <param name="rng">Random number generator</param>
    /// <param name="continuityTiles">Optional set of tiles that match collapsed neighbors (for continuity bias)</param>
    /// <param name="cellId">Cell ID for constraint context</param>
    /// <param name="topology">WFC topology for constraint context</param>
    /// <returns>Selected tile ID, or null if no valid tiles</returns>
    public string? SelectTile(
        IReadOnlyCollection<string> validTiles,
        BiomeDefinition? biome,
        RandomNumberGenerator rng,
        IReadOnlySet<string>? continuityTiles = null,
        int? cellId = null,
        IWfcTopology? topology = null)
    {
        if (validTiles.Count == 0)
            return null;

        // Fast path: single tile, no constraint evaluation needed
        if (validTiles.Count == 1)
        {
            // Direct access without LINQ for single-element case
            foreach (var tile in validTiles)
                return tile;
        }

        // One-time log to verify constraint count
        if (!_loggedConstraintCount && cellId.HasValue)
        {
            _loggedConstraintCount = true;
            GD.Print($"[WfcTileSelector] SelectTile called with cellId, constraints: {_constraints.Count}");
        }

        // Build weight lookup from biome
        var biomeWeights = BuildBiomeWeightLookup(biome);

        // Precompute collapsed neighbors ONCE for this cell
        Dictionary<int, string>? collapsedNeighbors = null;

        if (cellId.HasValue && topology != null && _constraints.Count > 0)
        {
            collapsedNeighbors = new Dictionary<int, string>(topology.MaxNeighborCount);

            // Use stack-allocated span for non-allocating neighbor iteration
            Span<int> neighborBuffer = stackalloc int[topology.MaxNeighborCount];
            var count = topology.GetNeighborsNonAlloc(cellId.Value, neighborBuffer);
            for (var i = 0; i < count; i++)
            {
                var neighborId = neighborBuffer[i];
                var neighborCell = topology.GetCell(neighborId);
                if (neighborCell.IsCollapsed())
                {
                    collapsedNeighbors[neighborId] = neighborCell.GetCollapsedTile();
                }
            }
        }

        // Calculate weighted probabilities
        var weights = new List<(string tileId, float weight)>(validTiles.Count);
        var totalWeight = 0f;

        foreach (var tileId in validTiles)
        {
            float weight;

            // Use uniform base weight (1.0) when cellId provided to prevent base weight
            // differences from causing fragmentation during spatial coherence growth
            if (cellId.HasValue && topology != null)
            {
                // Uniform weight to let spatial coherence dominate
                weight = 1.0f;
            }
            else if (biomeWeights.TryGetValue(tileId, out var biomeWeight))
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

            // Apply registered constraints with precomputed neighbor info
            if (cellId.HasValue && topology != null && _constraints.Count > 0)
            {
                var constraintContext = WfcConstraintContext.Create(
                    cellId.Value,
                    tileId,
                    topology,
                    rng,
                    collapsedNeighbors!);

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

        // Edge case: all weights are zero (all tiles banned by hard constraints)
        // Return null to signal contradiction - let WFC retry with different seed
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

        // Direct indexing for List, skip enumeration for small index values
        if (validTiles is IList<string> list)
            return list[index];

        // Fallback: iterate to index (avoids LINQ allocations)
        var i = 0;
        foreach (var tile in validTiles)
        {
            if (i == index)
                return tile;
            i++;
        }

        return null;
    }

    /// <summary>
    /// Computes weights for all valid tiles at a cell.
    /// Used for weighted entropy calculation during cell selection.
    /// </summary>
    /// <param name="validTiles">Tiles that satisfy hard constraints</param>
    /// <param name="biome">Current biome for soft rule weights</param>
    /// <param name="rng">Random number generator for constraint context</param>
    /// <param name="continuityTiles">Optional set of tiles matching collapsed neighbors</param>
    /// <param name="cellId">Cell ID for constraint evaluation</param>
    /// <param name="topology">WFC topology for constraint evaluation</param>
    /// <returns>Dictionary mapping tile IDs to their final weights</returns>
    public IReadOnlyDictionary<string, float> ComputeWeights(
        IReadOnlyCollection<string> validTiles,
        BiomeDefinition? biome,
        RandomNumberGenerator rng,
        IReadOnlySet<string>? continuityTiles = null,
        int? cellId = null,
        IWfcTopology? topology = null)
    {
        var weights = new Dictionary<string, float>(validTiles.Count);

        if (validTiles.Count == 0)
            return weights;

        var biomeWeights = BuildBiomeWeightLookup(biome);

        // Precompute collapsed neighbors ONCE for this cell
        Dictionary<int, string>? collapsedNeighbors = null;

        if (cellId.HasValue && topology != null && _constraints.Count > 0)
        {
            collapsedNeighbors = new Dictionary<int, string>(topology.MaxNeighborCount);

            // Use stack-allocated span for non-allocating neighbor iteration
            Span<int> neighborBuffer = stackalloc int[topology.MaxNeighborCount];
            var count = topology.GetNeighborsNonAlloc(cellId.Value, neighborBuffer);
            for (var i = 0; i < count; i++)
            {
                var neighborId = neighborBuffer[i];
                var neighborCell = topology.GetCell(neighborId);
                if (neighborCell.IsCollapsed())
                {
                    collapsedNeighbors[neighborId] = neighborCell.GetCollapsedTile();
                }
            }
        }

        foreach (var tileId in validTiles)
        {
            float weight;

            // Always start with biome weights to preserve intended tile distribution
            if (biomeWeights.TryGetValue(tileId, out var biomeWeight))
            {
                weight = biomeWeight;
            }
            else
            {
                weight = DefaultTileWeight * NonBiomeTilePenalty;
            }

            // Apply continuity bias
            if (continuityTiles != null && continuityTiles.Contains(tileId))
            {
                weight *= ContinuityBiasMultiplier;
            }

            // Apply constraints with precomputed neighbor info
            if (cellId.HasValue && topology != null && _constraints.Count > 0)
            {
                var constraintContext = WfcConstraintContext.Create(
                    cellId.Value,
                    tileId,
                    topology,
                    rng,
                    collapsedNeighbors!);

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

            weights[tileId] = weight;
        }

        return weights;
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
