using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Weight modifier that applies biome-specific tile affinity multipliers.
/// Tiles with higher affinity in the current biome become more likely.
/// </summary>
public sealed class BiomeAffinityModifier : IWeightModifier
{
    private readonly Dictionary<string, BiomeTileAffinity> _affinities = new();

    /// <summary>
    /// Register affinity configuration for a biome.
    /// </summary>
    public BiomeAffinityModifier WithAffinity(BiomeTileAffinity affinity)
    {
        _affinities[affinity.Biome] = affinity;
        return this;
    }

    /// <summary>
    /// Register multiple affinity configurations.
    /// </summary>
    public BiomeAffinityModifier WithAffinities(IEnumerable<BiomeTileAffinity> affinities)
    {
        foreach (var affinity in affinities)
            _affinities[affinity.Biome] = affinity;
        return this;
    }

    public void ApplyModifier(TileSelectionContext context)
    {
        // Get affinity config for current biome
        if (!_affinities.TryGetValue(context.CurrentBiome.Id, out var affinity))
            return; // No affinity defined - weights unchanged

        // Multiply each tile's weight by its affinity
        var affinityMap = affinity.GetAllAffinities();
        var weights = context.Weights;

        foreach (var tileId in new List<string>(weights.Keys))
        {
            var multiplier = affinityMap.GetValueOrDefault(tileId, 1.0f);
            weights[tileId] *= multiplier;
        }
    }
}
