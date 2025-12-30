using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.VariantModifiers;

/// <summary>
/// Context object containing all information needed by variant weight modifiers
/// when selecting which visual variant of a tile to use.
/// </summary>
public sealed class VariantSelectionContext
{
    private static readonly Vector2I[] CardinalDirections =
    [
        new(0, -1),  // North
        new(1, 0),   // East
        new(0, 1),   // South
        new(-1, 0)   // West
    ];

    public VariantSelectionContext(
        Vector2I position,
        IReadOnlyDictionary<Vector2I, string> placedTiles,
        BiomeDefinition currentBiome,
        TileDefinition tile,
        RandomNumberGenerator rng,
        float[] variantWeights)
    {
        ArgumentNullException.ThrowIfNull(placedTiles);
        ArgumentNullException.ThrowIfNull(currentBiome);
        ArgumentNullException.ThrowIfNull(tile);
        ArgumentNullException.ThrowIfNull(rng);
        ArgumentNullException.ThrowIfNull(variantWeights);

        Position = position;
        PlacedTiles = placedTiles;
        CurrentBiome = currentBiome;
        Tile = tile;
        Rng = rng;
        VariantWeights = variantWeights;
    }

    /// <summary>Current position being evaluated for variant selection.</summary>
    public Vector2I Position { get; }

    /// <summary>All tiles already placed on the map (immutable view).</summary>
    public IReadOnlyDictionary<Vector2I, string> PlacedTiles { get; }

    /// <summary>Biome at the current position.</summary>
    public BiomeDefinition CurrentBiome { get; }

    /// <summary>The tile definition whose variant is being selected.</summary>
    public TileDefinition Tile { get; }

    /// <summary>Random number generator for deterministic randomness.</summary>
    public RandomNumberGenerator Rng { get; }

    /// <summary>
    /// Mutable variant weights array - modifiers adjust these in-place.
    /// Index corresponds to Tile.Variations array index.
    /// </summary>
    public float[] VariantWeights { get; }

    /// <summary>Number of variants available for this tile.</summary>
    public int VariantCount => VariantWeights.Length;

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

    /// <summary>
    /// Check if any tiles within the given radius match the specified tile ID.
    /// </summary>
    /// <param name="tileId">The tile ID to search for.</param>
    /// <param name="radius">Maximum distance to search (Manhattan distance).</param>
    /// <returns>True if a matching tile is found within radius.</returns>
    public bool HasTileNearby(string tileId, int radius)
    {
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Math.Abs(dx) + Math.Abs(dy) > radius) continue;

                var checkPos = Position + new Vector2I(dx, dy);
                if (PlacedTiles.TryGetValue(checkPos, out var foundTileId) && foundTileId == tileId)
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Count tiles matching the specified ID within the given radius.
    /// </summary>
    /// <param name="tileId">The tile ID to count.</param>
    /// <param name="radius">Maximum distance to search (Manhattan distance).</param>
    /// <returns>Number of matching tiles found.</returns>
    public int CountTilesNearby(string tileId, int radius)
    {
        var count = 0;
        for (var dy = -radius; dy <= radius; dy++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                if (dx == 0 && dy == 0) continue;
                if (Math.Abs(dx) + Math.Abs(dy) > radius) continue;

                var checkPos = Position + new Vector2I(dx, dy);
                if (PlacedTiles.TryGetValue(checkPos, out var foundTileId) && foundTileId == tileId)
                    count++;
            }
        }
        return count;
    }
}
