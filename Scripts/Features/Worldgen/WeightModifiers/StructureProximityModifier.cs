using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Represents a placed structure's influence zone for weight modification.
/// </summary>
public sealed class PlacedStructureInfluence
{
    /// <summary>Anchor position of the structure.</summary>
    public required Vector2I Position { get; init; }

    /// <summary>Size of the structure footprint.</summary>
    public required Vector2I Size { get; init; }

    /// <summary>Radius of weight influence around the structure.</summary>
    public required int InfluenceRadius { get; init; }

    /// <summary>Tile affinity multipliers (tileId -> multiplier).</summary>
    public required IReadOnlyDictionary<string, float> TileAffinities { get; init; }
}

/// <summary>
/// Weight modifier that adjusts tile weights based on proximity to placed structures.
/// Each structure can define its own influence radius and tile affinities.
/// </summary>
public sealed class StructureProximityModifier : IWeightModifier
{
    private readonly List<PlacedStructureInfluence> _placedStructures = [];

    /// <summary>
    /// Minimum weight multiplier (prevents complete exclusion).
    /// </summary>
    public float MinWeight { get; set; } = 0.1f;

    /// <summary>
    /// Get all registered structure positions (for spacing calculations).
    /// </summary>
    public IReadOnlyList<PlacedStructureInfluence> PlacedStructures => _placedStructures;

    /// <summary>
    /// Register a placed structure for proximity-based weight modification.
    /// </summary>
    public void RegisterStructure(PlacedStructureInfluence influence)
    {
        ArgumentNullException.ThrowIfNull(influence);
        _placedStructures.Add(influence);
    }

    /// <summary>
    /// Register a structure from a stamp at a given position.
    /// </summary>
    public void RegisterStructure(Vector2I position, Structures.StructureStamp stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);

        var affinities = new Dictionary<string, float>();
        foreach (var entry in stamp.TileAffinities)
        {
            if (!string.IsNullOrEmpty(entry.TileId))
                affinities[entry.TileId] = entry.Affinity;
        }

        _placedStructures.Add(new PlacedStructureInfluence
        {
            Position = position,
            Size = stamp.Size,
            InfluenceRadius = stamp.InfluenceRadius,
            TileAffinities = affinities
        });
    }

    /// <summary>
    /// Clear all registered structures.
    /// </summary>
    public void Clear() => _placedStructures.Clear();

    public void ApplyModifier(TileSelectionContext context)
    {
        if (_placedStructures.Count == 0)
            return;

        var position = context.Position;
        var weights = context.Weights;

        // Check each placed structure
        foreach (var structure in _placedStructures)
        {
            // Calculate distance to nearest point of structure footprint
            var distance = CalculateDistanceToStructure(position, structure);

            // Skip if outside influence radius
            if (distance > structure.InfluenceRadius)
                continue;

            // Apply tile affinities with distance falloff
            ApplyAffinities(weights, structure.TileAffinities, distance, structure.InfluenceRadius);
        }
    }

    /// <summary>
    /// Calculate the minimum distance from a position to any cell in a structure's footprint.
    /// </summary>
    private static float CalculateDistanceToStructure(Vector2I position, PlacedStructureInfluence structure)
    {
        // Clamp position to structure bounds to find nearest point
        var clampedX = Math.Clamp(position.X, structure.Position.X, structure.Position.X + structure.Size.X - 1);
        var clampedY = Math.Clamp(position.Y, structure.Position.Y, structure.Position.Y + structure.Size.Y - 1);

        var dx = position.X - clampedX;
        var dy = position.Y - clampedY;

        // Return 0 if inside structure footprint, otherwise Euclidean distance
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Apply tile affinities with linear distance falloff.
    /// </summary>
    private void ApplyAffinities(
        Dictionary<string, float> weights,
        IReadOnlyDictionary<string, float> affinities,
        float distance,
        int influenceRadius)
    {
        if (affinities.Count == 0)
            return;

        // Calculate falloff: stronger effect closer to structure
        // At distance 0: full effect (1.0), at influence radius: no effect (0.0)
        var normalizedDistance = distance / influenceRadius;
        var falloff = 1.0f - normalizedDistance;

        foreach (var (tileId, affinity) in affinities)
        {
            if (!weights.TryGetValue(tileId, out var currentWeight))
                continue;

            // Interpolate multiplier based on falloff
            // falloff=1.0 → full affinity, falloff=0.0 → no change (1.0)
            var effectiveMultiplier = 1.0f + (affinity - 1.0f) * falloff;

            weights[tileId] = MathF.Max(currentWeight * effectiveMultiplier, MinWeight);
        }
    }
}
