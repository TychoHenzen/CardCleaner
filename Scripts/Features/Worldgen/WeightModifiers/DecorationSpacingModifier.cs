using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Weight modifier that reduces decoration tile weights based on proximity to other decorations.
/// Same-type decorations use a larger exclusion radius to prevent clustering.
/// </summary>
public sealed class DecorationSpacingModifier : IWeightModifier
{
    private readonly HashSet<string> _decorationTileIds;
    private readonly Func<string, string>? _getDecorationGroup;

    /// <summary>
    /// Radius (in tiles) to check for any decoration.
    /// Decorations within this radius reduce weight of all decoration tiles.
    /// </summary>
    public int BaseRadius { get; set; } = 2;

    /// <summary>
    /// Radius (in tiles) to check for same-type decorations.
    /// Same decorations within this radius have stronger weight reduction.
    /// </summary>
    public int SameTypeRadius { get; set; } = 4;

    /// <summary>
    /// Minimum weight multiplier (prevents complete exclusion).
    /// </summary>
    public float MinWeight { get; set; } = 0.1f;

    /// <summary>
    /// Create modifier with decoration tile IDs.
    /// </summary>
    /// <param name="decorationTileIds">Set of tile IDs that are considered decorations.</param>
    /// <param name="getDecorationGroup">Optional function to group similar decorations.</param>
    public DecorationSpacingModifier(
        HashSet<string> decorationTileIds,
        Func<string, string>? getDecorationGroup = null)
    {
        _decorationTileIds = decorationTileIds;
        _getDecorationGroup = getDecorationGroup;
    }

    public void ApplyModifier(TileSelectionContext context)
    {
        if (_decorationTileIds.Count == 0)
            return;

        // Find nearby decorations
        var nearbyDecorations = FindNearbyDecorations(context.Position, context.PlacedTiles);
        if (nearbyDecorations.Count == 0)
            return;

        // Apply weight reduction to decoration tiles
        var weights = context.Weights;
        foreach (var tileId in new List<string>(weights.Keys))
        {
            if (!_decorationTileIds.Contains(tileId))
                continue;

            var reduction = CalculateWeightReduction(tileId, nearbyDecorations);
            weights[tileId] *= reduction;
        }
    }

    private List<(string TileId, float Distance)> FindNearbyDecorations(
        Vector2I position,
        IReadOnlyDictionary<Vector2I, string> placedTiles)
    {
        var result = new List<(string, float)>();
        var maxRadius = Math.Max(BaseRadius, SameTypeRadius);

        for (var dy = -maxRadius; dy <= maxRadius; dy++)
        {
            for (var dx = -maxRadius; dx <= maxRadius; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                var checkPos = position + new Vector2I(dx, dy);
                if (!placedTiles.TryGetValue(checkPos, out var tileId))
                    continue;

                if (!_decorationTileIds.Contains(tileId))
                    continue;

                var distance = MathF.Sqrt(dx * dx + dy * dy);
                result.Add((tileId, distance));
            }
        }

        return result;
    }

    private float CalculateWeightReduction(
        string candidateTileId,
        List<(string TileId, float Distance)> nearbyDecorations)
    {
        var multiplier = 1.0f;
        var candidateGroup = GetGroup(candidateTileId);

        foreach (var (placedTileId, distance) in nearbyDecorations)
        {
            var placedGroup = GetGroup(placedTileId);
            var isSameType = candidateGroup == placedGroup;

            // Use appropriate radius
            var effectiveRadius = isSameType ? SameTypeRadius : BaseRadius;
            if (distance > effectiveRadius)
                continue;

            // Linear falloff: closer = stronger reduction
            var normalizedDistance = distance / effectiveRadius;
            var reduction = isSameType
                ? 0.2f + 0.8f * normalizedDistance  // Same type: stronger (0.2 to 1.0)
                : 0.5f + 0.5f * normalizedDistance; // Any decoration: lighter (0.5 to 1.0)

            multiplier *= reduction;
        }

        return MathF.Max(multiplier, MinWeight);
    }

    private string GetGroup(string tileId)
    {
        return _getDecorationGroup?.Invoke(tileId) ?? tileId;
    }
}
