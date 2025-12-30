using System.Collections.Generic;
using CardCleaner.Scripts.Core.Enumeration;

namespace CardCleaner.Scripts.Features.Worldgen.VariantModifiers;

/// <summary>
/// Variant weight modifier that applies biome-specific variant preferences.
/// E.g., "grass" tile might prefer variant 0 (lush) in forests but variant 2 (dry) in deserts.
/// </summary>
public sealed class BiomeVariantModifier : IVariantWeightModifier
{
    private readonly Dictionary<string, Dictionary<BiomeType, BiomeVariantPreference>> _preferences = new();

    /// <summary>
    /// Configure variant preferences for a tile in a specific biome.
    /// </summary>
    /// <param name="tileId">The base tile ID (e.g., "grass").</param>
    /// <param name="biome">The biome where these preferences apply.</param>
    /// <param name="variantMultipliers">Multipliers indexed by variant index. Missing indices = 1.0.</param>
    /// <returns>This modifier for fluent configuration.</returns>
    public BiomeVariantModifier WithPreference(string tileId, BiomeType biome, params float[] variantMultipliers)
    {
        if (!_preferences.TryGetValue(tileId, out var tilePrefs))
        {
            tilePrefs = new Dictionary<BiomeType, BiomeVariantPreference>();
            _preferences[tileId] = tilePrefs;
        }

        tilePrefs[biome] = new BiomeVariantPreference(variantMultipliers);
        return this;
    }

    /// <summary>
    /// Configure variant preferences using named variants (if tile has variant names defined).
    /// </summary>
    public BiomeVariantModifier WithPreference(
        string tileId,
        BiomeType biome,
        Dictionary<int, float> indexedMultipliers)
    {
        if (!_preferences.TryGetValue(tileId, out var tilePrefs))
        {
            tilePrefs = new Dictionary<BiomeType, BiomeVariantPreference>();
            _preferences[tileId] = tilePrefs;
        }

        tilePrefs[biome] = new BiomeVariantPreference(indexedMultipliers);
        return this;
    }

    public void ApplyModifier(VariantSelectionContext context)
    {
        var tileId = context.Tile.Id;
        var biome = context.CurrentBiome.Type;

        // Check if we have preferences for this tile
        if (!_preferences.TryGetValue(tileId, out var tilePrefs))
            return;

        // Check if we have preferences for this biome
        if (!tilePrefs.TryGetValue(biome, out var preference))
            return;

        // Apply multipliers to variant weights
        for (var i = 0; i < context.VariantCount; i++)
        {
            context.VariantWeights[i] *= preference.GetMultiplier(i);
        }
    }
}

/// <summary>
/// Stores variant weight multipliers for a specific tile in a specific biome.
/// </summary>
public sealed class BiomeVariantPreference
{
    private readonly Dictionary<int, float> _multipliers = new();

    /// <summary>
    /// Create from array of multipliers (index = variant index).
    /// </summary>
    public BiomeVariantPreference(float[] multipliers)
    {
        for (var i = 0; i < multipliers.Length; i++)
        {
            _multipliers[i] = multipliers[i];
        }
    }

    /// <summary>
    /// Create from dictionary of index -> multiplier.
    /// </summary>
    public BiomeVariantPreference(Dictionary<int, float> multipliers)
    {
        foreach (var (index, mult) in multipliers)
        {
            _multipliers[index] = mult;
        }
    }

    /// <summary>
    /// Get the multiplier for a variant index. Returns 1.0 if not configured.
    /// </summary>
    public float GetMultiplier(int variantIndex)
    {
        return _multipliers.GetValueOrDefault(variantIndex, 1.0f);
    }
}
