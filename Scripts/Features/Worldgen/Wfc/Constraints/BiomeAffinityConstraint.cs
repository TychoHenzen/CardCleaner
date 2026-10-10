using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Adjusts tile probability based on biome strength at position.
/// Tiles allowed in biomes with higher strength at a position get boosted.
/// </summary>
/// <remarks>
/// For each biome the tile is allowed in (via TileDefinition.AllowedBiomes),
/// the constraint calculates: 1.0 + (biomeStrength * boostFactor).
/// For tiles in multiple biomes, uses the maximum strength to prevent dilution.
/// Universal tiles (AllowedBiomes == null) get neutral weighting.
/// </remarks>
public class BiomeAffinityConstraint : IWfcConstraint
{
    private readonly BiomeStrengthGrid _grid;
    private readonly BiomeRegistry _registry;
    private readonly IWfcTileCatalog? _tileCatalog;
    private readonly Dictionary<string, List<string>> _tileToBiomes;

    /// <summary>Controls how biome strength scales tile probability.</summary>
    public float BoostFactor { get; set; } = 2.0f;

    /// <summary>
    /// Minimum probability modifier to prevent tiles from being completely eliminated.
    /// </summary>
    public float MinModifier { get; set; } = 0.1f;

    public BiomeAffinityConstraint(BiomeStrengthGrid grid, BiomeRegistry registry, IWfcTileCatalog? tileCatalog = null)
    {
        _grid = grid;
        _registry = registry;
        _tileCatalog = tileCatalog;
        _tileToBiomes = BuildTileToBiomesMap();
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // If tile doesn't belong to any biome, return neutral
        if (!_tileToBiomes.TryGetValue(context.TileId, out var biomeIds) || biomeIds.Count == 0)
            return 1.0f;

        // BiomeStrengthGrid uses Vector2I positions - only works for grid topologies
        // For non-grid topologies, return neutral (biome affinity not supported)
        if (context.Topology is not WfcGrid grid)
            return 1.0f;

        var position = grid.CellIdToPosition(context.CellId);

        // Find maximum strength across all biomes this tile belongs to
        // Using max instead of average prevents dilution for tiles in multiple biomes
        var maxStrength = float.MinValue;

        foreach (var biomeId in biomeIds)
        {
            var strength = _grid.GetStrength(position, biomeId);
            if (strength > maxStrength)
                maxStrength = strength;
        }

        if (maxStrength == float.MinValue)
            return 1.0f;

        // Convert strength to probability modifier
        // strength +1 → modifier = 1 + BoostFactor = 3.0 (strong boost)
        // strength 0  → modifier = 1.0 (neutral)
        // strength -1 → modifier = 1 - BoostFactor = -1.0, clamped to MinModifier = 0.1 (strong penalty)
        var modifier = 1.0f + maxStrength * BoostFactor;

        return Mathf.Max(MinModifier, modifier);
    }

    /// <summary>
    /// Builds a lookup from tile ID to list of biome IDs that the tile is allowed in.
    /// Uses TileDefinition.AllowedBiomes instead of explicit biome tile pools.
    /// </summary>
    private Dictionary<string, List<string>> BuildTileToBiomesMap()
    {
        var map = new Dictionary<string, List<string>>();
        var allBiomeIds = _registry.GetAllBiomeIds().ToList();

        if (_tileCatalog == null)
            return map;

        // For each tile, check which biomes it's allowed in
        foreach (var tileId in _tileCatalog.TileIds)
        {
            var biomeList = new List<string>();

            // If AllowedBiomes is null, tile is universal - don't add to map (gets neutral weighting)
            // If AllowedBiomes has specific biomes, add those
            if (_tileCatalog.HasBiomeRestriction(tileId))
            {
                foreach (var biomeId in allBiomeIds)
                {
                    if (_tileCatalog.IsAllowedInBiome(tileId, biomeId))
                        biomeList.Add(biomeId);
                }
            }

            if (biomeList.Count > 0)
                map[tileId] = biomeList;
        }

        return map;
    }
}
