using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.VariantModifiers;

/// <summary>
/// Variant weight modifier that adjusts variant probabilities based on proximity to specific tiles.
/// E.g., "stone_mossy" variant is more likely near water tiles.
/// </summary>
public sealed class ProximityVariantModifier : IVariantWeightModifier
{
    private readonly List<ProximityRule> _rules = [];

    /// <summary>
    /// Add a proximity rule that boosts a variant when near certain tiles.
    /// </summary>
    /// <param name="tileId">The base tile ID this rule applies to.</param>
    /// <param name="variantIndex">The variant index to affect.</param>
    /// <param name="nearTileId">The tile ID that triggers the boost when nearby.</param>
    /// <param name="radius">Maximum distance to search for the trigger tile (Manhattan distance).</param>
    /// <param name="multiplier">Weight multiplier when trigger tile is found (e.g., 2.0 for 2x boost).</param>
    /// <returns>This modifier for fluent configuration.</returns>
    public ProximityVariantModifier WithRule(
        string tileId,
        int variantIndex,
        string nearTileId,
        int radius,
        float multiplier)
    {
        _rules.Add(new ProximityRule(tileId, variantIndex, nearTileId, radius, multiplier, isInverseRule: false));
        return this;
    }

    /// <summary>
    /// Add a proximity rule that boosts a variant when NOT near certain tiles (inverse).
    /// </summary>
    /// <param name="tileId">The base tile ID this rule applies to.</param>
    /// <param name="variantIndex">The variant index to affect.</param>
    /// <param name="farFromTileId">The tile ID that suppresses this variant when nearby.</param>
    /// <param name="radius">Distance within which the tile must NOT be present.</param>
    /// <param name="multiplier">Weight multiplier when trigger tile is NOT found (e.g., 2.0 for 2x boost).</param>
    /// <returns>This modifier for fluent configuration.</returns>
    public ProximityVariantModifier WithInverseRule(
        string tileId,
        int variantIndex,
        string farFromTileId,
        int radius,
        float multiplier)
    {
        _rules.Add(new ProximityRule(tileId, variantIndex, farFromTileId, radius, multiplier, isInverseRule: true));
        return this;
    }

    /// <summary>
    /// Add a gradient rule that scales multiplier based on distance.
    /// Closer tiles get higher multipliers, farther tiles get lower.
    /// </summary>
    /// <param name="tileId">The base tile ID this rule applies to.</param>
    /// <param name="variantIndex">The variant index to affect.</param>
    /// <param name="nearTileId">The tile ID to measure distance from.</param>
    /// <param name="maxRadius">Maximum distance to search.</param>
    /// <param name="maxMultiplier">Multiplier when adjacent (distance 1).</param>
    /// <param name="minMultiplier">Multiplier at max radius.</param>
    /// <returns>This modifier for fluent configuration.</returns>
    public ProximityVariantModifier WithGradientRule(
        string tileId,
        int variantIndex,
        string nearTileId,
        int maxRadius,
        float maxMultiplier,
        float minMultiplier = 1.0f)
    {
        _rules.Add(new ProximityRule(tileId, variantIndex, nearTileId, maxRadius, maxMultiplier, minMultiplier));
        return this;
    }

    public void ApplyModifier(VariantSelectionContext context)
    {
        var tileId = context.Tile.Id;

        foreach (var rule in _rules)
        {
            if (rule.TileId != tileId)
                continue;

            if (rule.VariantIndex >= context.VariantCount)
                continue;

            var multiplier = rule.CalculateMultiplier(context);
            context.VariantWeights[rule.VariantIndex] *= multiplier;
        }
    }
}

/// <summary>
/// A single proximity-based variant weight rule.
/// </summary>
internal sealed class ProximityRule
{
    public string TileId { get; }
    public int VariantIndex { get; }
    public string TriggerTileId { get; }
    public int Radius { get; }
    public float MaxMultiplier { get; }
    public float MinMultiplier { get; }
    public bool IsInverseRule { get; }
    public bool IsGradientRule { get; }

    /// <summary>
    /// Create a simple proximity rule (binary: near or not near).
    /// </summary>
    public ProximityRule(
        string tileId,
        int variantIndex,
        string triggerTileId,
        int radius,
        float multiplier,
        bool isInverseRule)
    {
        TileId = tileId;
        VariantIndex = variantIndex;
        TriggerTileId = triggerTileId;
        Radius = radius;
        MaxMultiplier = multiplier;
        MinMultiplier = 1.0f;
        IsInverseRule = isInverseRule;
        IsGradientRule = false;
    }

    /// <summary>
    /// Create a gradient proximity rule (scales with distance).
    /// </summary>
    public ProximityRule(
        string tileId,
        int variantIndex,
        string triggerTileId,
        int radius,
        float maxMultiplier,
        float minMultiplier)
    {
        TileId = tileId;
        VariantIndex = variantIndex;
        TriggerTileId = triggerTileId;
        Radius = radius;
        MaxMultiplier = maxMultiplier;
        MinMultiplier = minMultiplier;
        IsInverseRule = false;
        IsGradientRule = true;
    }

    public float CalculateMultiplier(VariantSelectionContext context)
    {
        if (IsGradientRule)
        {
            return CalculateGradientMultiplier(context);
        }

        var hasNearby = context.HasTileNearby(TriggerTileId, Radius);

        if (IsInverseRule)
        {
            // Inverse: boost when NOT near
            return hasNearby ? 1.0f : MaxMultiplier;
        }

        // Normal: boost when near
        return hasNearby ? MaxMultiplier : 1.0f;
    }

    private float CalculateGradientMultiplier(VariantSelectionContext context)
    {
        // Find the closest matching tile
        var closestDistance = int.MaxValue;

        for (var dy = -Radius; dy <= Radius; dy++)
        {
            for (var dx = -Radius; dx <= Radius; dx++)
            {
                if (dx == 0 && dy == 0) continue;

                var distance = System.Math.Abs(dx) + System.Math.Abs(dy);
                if (distance > Radius || distance >= closestDistance) continue;

                var checkPos = context.Position + new Godot.Vector2I(dx, dy);
                if (context.PlacedTiles.TryGetValue(checkPos, out var foundTileId) && foundTileId == TriggerTileId)
                {
                    closestDistance = distance;
                }
            }
        }

        if (closestDistance == int.MaxValue)
        {
            // No matching tile found within radius
            return 1.0f;
        }

        // Interpolate multiplier based on distance (closer = higher multiplier)
        // distance 1 -> MaxMultiplier, distance Radius -> MinMultiplier
        var t = (float)(closestDistance - 1) / (Radius - 1);
        return MaxMultiplier + t * (MinMultiplier - MaxMultiplier);
    }
}
