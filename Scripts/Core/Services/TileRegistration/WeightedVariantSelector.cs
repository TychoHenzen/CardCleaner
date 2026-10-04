using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Picks one variant from a group by weighted random selection.
/// </summary>
internal static class WeightedVariantSelector
{
    internal static string? Select(VariationGroup? group, RandomNumberGenerator rng)
    {
        if (group == null || group.Variants.Count == 0)
            return null;

        // Weighted random selection
        var totalWeight = 0f;
        foreach (var v in group.Variants)
            totalWeight += v.Weight;

        if (totalWeight <= 0)
            return group.Variants[0].TileId;

        var roll = rng.Randf() * totalWeight;
        var cumulative = 0f;
        foreach (var v in group.Variants)
        {
            cumulative += v.Weight;
            if (roll <= cumulative)
                return v.TileId;
        }

        return group.Variants[^1].TileId;
    }
}
