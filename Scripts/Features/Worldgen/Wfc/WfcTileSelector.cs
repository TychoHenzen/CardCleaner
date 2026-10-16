using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Selectors;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Selects tiles from valid options using weighted probabilities.
/// With a cell and topology, every valid tile gets a uniform base weight, so biome pools do not choose the tile.
/// ComputeWeights, which feeds the weighted entropy, still applies the biome pool weights.
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
        ILog.Print($"[WfcTileSelector] Added constraint: {constraint.GetType().Name}, total: {_constraints.Count}");
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
        ILog.Print($"[WfcTileSelector] ClearConstraints called, had {_constraints.Count} constraints");
        _constraints.Clear();
        _loggedConstraintCount = false;
    }

    /// <summary>
    /// Penalty for tiles outside the biome pool. ComputeWeights always applies it; SelectTile only without a cell and
    /// topology, so the solver's tile choice does not depend on it.
    /// </summary>
    public float NonBiomeTilePenalty { get; set; } = 0.1f;

    /// <summary>
    /// Default weight for tiles not explicitly defined in biome pools.
    /// </summary>
    public float DefaultTileWeight { get; set; } = 1.0f;

    /// <summary>Weights tiles matching collapsed neighbors; 1 disables continuity bias.</summary>
    public float ContinuityBiasMultiplier { get; set; } = 2.0f;

    /// <summary>
    /// Selects a tile from the valid options. With a cell and topology, every valid tile gets a uniform base weight,
    /// scaled by the registered constraints and the continuity bias; biome weights are not used on this path.
    /// Without them, each tile gets its biome pool weight, or DefaultTileWeight times NonBiomeTilePenalty.
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
        IRandom rng,
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

        LogConstraintCount(cellId);
        var weightContext = CreateWeightContext(
            biome,
            rng,
            continuityTiles,
            cellId,
            topology,
            useUniformBaseWeight: cellId.HasValue && topology != null);
        var weights = WfcWeightedCandidateBuilder.BuildWeightedCandidates(
            validTiles,
            weightContext);

        return WfcWeightedTileChooser.SelectWeightedTile(weights, rng);
    }

    /// <summary>
    /// Selects a tile with uniform probability (no biome weighting).
    /// Useful for testing or biome-agnostic selection.
    /// </summary>
    public string? SelectUniform(IReadOnlyCollection<string> validTiles, IRandom rng)
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
        IRandom rng,
        IReadOnlySet<string>? continuityTiles = null,
        int? cellId = null,
        IWfcTopology? topology = null)
    {
        var weights = new Dictionary<string, float>(validTiles.Count);
        if (validTiles.Count == 0)
            return weights;

        var weightContext = CreateWeightContext(
            biome,
            rng,
            continuityTiles,
            cellId,
            topology,
            useUniformBaseWeight: false);
        return WfcWeightedCandidateBuilder.BuildWeightLookup(
            validTiles,
            weightContext);
    }

    private WfcTileWeightContext CreateWeightContext(
        BiomeDefinition? biome,
        IRandom rng,
        IReadOnlySet<string>? continuityTiles,
        int? cellId,
        IWfcTopology? topology,
        bool useUniformBaseWeight)
    {
        var biomeWeights = WfcWeightedCandidateBuilder.BuildBiomeWeightLookup(biome);
        var collapsedNeighbors = WfcWeightedCandidateBuilder.BuildCollapsedNeighbors(
            cellId,
            topology,
            _constraints);
        var collapsedWindowNeighbors = WfcWeightedCandidateBuilder.BuildCollapsedWindowNeighbors(
            cellId,
            topology,
            _constraints);
        return new WfcTileWeightContext(
            biomeWeights,
            rng,
            continuityTiles,
            cellId,
            topology,
            collapsedNeighbors,
            collapsedWindowNeighbors,
            useUniformBaseWeight,
            _constraints,
            DefaultTileWeight,
            NonBiomeTilePenalty,
            ContinuityBiasMultiplier);
    }

    private void LogConstraintCount(int? cellId)
    {
        if (_loggedConstraintCount || !cellId.HasValue)
            return;

        _loggedConstraintCount = true;
        ILog.Print($"[WfcTileSelector] SelectTile called with cellId, constraints: {_constraints.Count}");
        foreach (var constraint in _constraints)
            ILog.Print($"  - {constraint.GetType().Name}");
    }
}
