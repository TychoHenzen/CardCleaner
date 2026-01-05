using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Adjusts tile probability based on biome strength at position.
/// Tiles belonging to biomes with higher strength at a position get boosted.
/// </summary>
/// <remarks>
/// For each biome the tile belongs to (via PassableTiles or BlockedTiles),
/// the constraint calculates: 1.0 + (biomeStrength * boostFactor).
/// For tiles in multiple biomes, uses the maximum strength to prevent dilution.
/// </remarks>
public class BiomeAffinityConstraint : IWfcConstraint
{
    private readonly BiomeStrengthGrid _grid;
    private readonly BiomeRegistry _registry;
    private readonly Dictionary<string, List<string>> _tileToBiomes;

    /// <summary>
    /// Factor controlling how much biome strength affects probability.
    /// Default 2.0 means: +1 strength → 3.0x probability, -1 strength → 0.1x penalty (MinModifier clamp).
    /// Increased from 0.5 to overcome continuity bias and create coherent biome regions.
    /// </summary>
    public float BoostFactor { get; set; } = 2.0f;

    /// <summary>
    /// Minimum probability modifier to prevent tiles from being completely eliminated.
    /// </summary>
    public float MinModifier { get; set; } = 0.1f;

    public BiomeAffinityConstraint(BiomeStrengthGrid grid, BiomeRegistry registry)
    {
        _grid = grid;
        _registry = registry;
        _tileToBiomes = BuildTileToBiomesMap();
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // If tile doesn't belong to any biome, return neutral
        if (!_tileToBiomes.TryGetValue(context.TileId, out var biomeIds) || biomeIds.Count == 0)
            return 1.0f;

        // Find maximum strength across all biomes this tile belongs to
        // Using max instead of average prevents dilution for tiles in multiple biomes
        var maxStrength = float.MinValue;

        foreach (var biomeId in biomeIds)
        {
            var strength = _grid.GetStrength(context.Position, biomeId);
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
    /// Builds a lookup from tile ID to list of biome IDs that contain it.
    /// </summary>
    private Dictionary<string, List<string>> BuildTileToBiomesMap()
    {
        var map = new Dictionary<string, List<string>>();

        foreach (var biome in _registry.GetAllBiomes())
        {
            // Add passable tiles
            foreach (var tileId in biome.PassableTiles.GetAllTileIds())
            {
                if (!map.TryGetValue(tileId, out var biomeList))
                {
                    biomeList = new List<string>();
                    map[tileId] = biomeList;
                }
                if (!biomeList.Contains(biome.Id))
                    biomeList.Add(biome.Id);
            }

            // Add blocked tiles
            foreach (var tileId in biome.BlockedTiles.GetAllTileIds())
            {
                if (!map.TryGetValue(tileId, out var biomeList))
                {
                    biomeList = new List<string>();
                    map[tileId] = biomeList;
                }
                if (!biomeList.Contains(biome.Id))
                    biomeList.Add(biome.Id);
            }
        }

        return map;
    }
}
