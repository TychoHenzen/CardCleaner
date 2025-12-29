using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Context object containing all information needed by weight modifiers during tile selection.
/// </summary>
public sealed class TileSelectionContext
{
    private static readonly Vector2I[] CardinalDirections =
    [
        new(0, -1),  // North
        new(1, 0),   // East
        new(0, 1),   // South
        new(-1, 0)   // West
    ];

    public TileSelectionContext(
        Vector2I position,
        IReadOnlyDictionary<Vector2I, string> placedTiles,
        BiomeDefinition currentBiome,
        RandomNumberGenerator rng,
        Dictionary<string, float> weights)
    {
        ArgumentNullException.ThrowIfNull(placedTiles);
        ArgumentNullException.ThrowIfNull(currentBiome);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(weights);

        Position = position;
        PlacedTiles = placedTiles;
        CurrentBiome = currentBiome;
        Rng = rng;
        Weights = weights;
    }

    /// <summary>Current position being evaluated for tile placement.</summary>
    public Vector2I Position { get; }

    /// <summary>All tiles already placed on the map (immutable view).</summary>
    public IReadOnlyDictionary<Vector2I, string> PlacedTiles { get; }

    /// <summary>Biome at the current position.</summary>
    public BiomeDefinition CurrentBiome { get; }

    /// <summary>Random number generator for deterministic randomness.</summary>
    public RandomNumberGenerator Rng { get; }

    /// <summary>Mutable weights dictionary - modifiers adjust these in-place.</summary>
    public Dictionary<string, float> Weights { get; }

    /// <summary>
    /// Get all cardinal neighbor positions (N, E, S, W).
    /// </summary>
    public IReadOnlyList<Vector2I> GetNeighbors()
    {
        var neighbors = new Vector2I[4];
        for (var i = 0; i < CardinalDirections.Length; i++)
        {
            neighbors[i] = Position + CardinalDirections[i];
        }
        return neighbors;
    }

    /// <summary>
    /// Get all placed neighbor tiles with their positions.
    /// Only returns neighbors that have tiles placed.
    /// </summary>
    public IReadOnlyList<(Vector2I Position, string TileId)> GetNeighborTiles()
    {
        var neighborTiles = new List<(Vector2I, string)>(4);
        foreach (var direction in CardinalDirections)
        {
            var neighborPos = Position + direction;
            if (PlacedTiles.TryGetValue(neighborPos, out var tileId))
            {
                neighborTiles.Add((neighborPos, tileId));
            }
        }
        return neighborTiles;
    }

    /// <summary>
    /// Get tile at a relative offset from current position.
    /// </summary>
    /// <returns>Tile ID if placed, null otherwise.</returns>
    public string? GetTileAt(Vector2I offset)
    {
        var targetPosition = Position + offset;
        return PlacedTiles.TryGetValue(targetPosition, out var tileId) ? tileId : null;
    }
}
