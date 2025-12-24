using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Creates a simple connected map of passable/blocked tiles using tile IDs
/// </summary>
public class SimpleMapGenerator
{
    private readonly RandomNumberGenerator _rng;

    public const string FloorTileId = "floor";
    public const string WallTileId = "wall";
    public const string GrassTileId = "grass";
    public const string DirtTileId = "dirt";
    public const string StoneTileId = "stone";
    public const string WaterTileId = "water";

    public SimpleMapGenerator(RandomNumberGenerator rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        _rng = rng;
    }

    public SimpleMapData GenerateMap(Vector2I size, CardSignature mapSeed, float blockedPercentage = 0.3f)
    {
        ILog.Print($"Generating simple map {size.X}x{size.Y} with {blockedPercentage:P0} blocked tiles");

        var tileIds = new string[size.Y, size.X];
        var passableTiles = new List<Vector2I>();

        // First pass: randomly place blocked tiles with varied terrain
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var isBlocked = _rng.Randf() < blockedPercentage;
            var position = new Vector2I(x, y);

            if (isBlocked)
            {
                tileIds[y, x] = SelectBlockedTile(position, size, mapSeed);
            }
            else
            {
                tileIds[y, x] = SelectPassableTile(position, size, mapSeed);
                passableTiles.Add(position);
            }
        }

        // Ensure we have at least some passable tiles
        if (passableTiles.Count == 0)
        {
            var center = new Vector2I(size.X / 2, size.Y / 2);
            tileIds[center.Y, center.X] = FloorTileId;
            passableTiles.Add(center);
        }

        // Ensure all passable tiles are connected
        EnsureConnectivity(tileIds, size, passableTiles);

        // Choose random positions for player and enemies
        var shuffledTiles = passableTiles.OrderBy(_ => _rng.Randf()).ToList();
        var playerStart = shuffledTiles[0];

        // Place 2-3 enemies randomly
        var enemyCount = _rng.RandiRange(2, Mathf.Min(3, shuffledTiles.Count - 1));
        var enemyPositions = shuffledTiles.Skip(1).Take(enemyCount).ToList();

        ILog.Print(
            $"Map generated: {passableTiles.Count} passable tiles, player at {playerStart}, {enemyCount} enemies");

        return new SimpleMapData
        {
            TileIds = tileIds,
            Size = size,
            PlayerStart = playerStart,
            EnemyPositions = enemyPositions,
            PassableTiles = passableTiles
        };
    }

    private void EnsureConnectivity(string[,] tileIds, Vector2I size, List<Vector2I> passableTiles)
    {
        if (passableTiles.Count <= 1) return;

        var visited = new bool[size.Y, size.X];
        var components = new List<List<Vector2I>>();

        foreach (var tile in passableTiles)
        {
            if (visited[tile.Y, tile.X]) continue;

            var component = new List<Vector2I>();
            FloodFill(tileIds, visited, size, tile, component);
            if (component.Count > 0) components.Add(component);
        }

        while (components.Count > 1)
        {
            var component1 = components[0];
            var component2 = components[1];

            var closest1 = component1[0];
            var closest2 = component2[0];
            var minDistance = float.MaxValue;

            foreach (var tile1 in component1)
            foreach (var tile2 in component2)
            {
                var distance = tile1.DistanceTo(tile2);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest1 = tile1;
                    closest2 = tile2;
                }
            }

            CreateCorridor(tileIds, size, closest1, closest2);

            component1.AddRange(component2);
            components.RemoveAt(1);

            passableTiles.Clear();
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
                if (IsPassableTile(tileIds[y, x]))
                    passableTiles.Add(new Vector2I(x, y));
        }

        ILog.Print($"Connectivity ensured: {components.Count} connected component(s)");
    }

    private void FloodFill(string[,] tileIds, bool[,] visited, Vector2I size, Vector2I start, List<Vector2I> component)
    {
        var stack = new Stack<Vector2I>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.X < 0 || current.X >= size.X ||
                current.Y < 0 || current.Y >= size.Y ||
                visited[current.Y, current.X] ||
                !IsPassableTile(tileIds[current.Y, current.X]))
                continue;

            visited[current.Y, current.X] = true;
            component.Add(current);

            stack.Push(new Vector2I(current.X + 1, current.Y));
            stack.Push(new Vector2I(current.X - 1, current.Y));
            stack.Push(new Vector2I(current.X, current.Y + 1));
            stack.Push(new Vector2I(current.X, current.Y - 1));
        }
    }

    private void CreateCorridor(string[,] tileIds, Vector2I size, Vector2I from, Vector2I to)
    {
        var current = from;

        while (current.X != to.X)
        {
            current.X += current.X < to.X ? 1 : -1;
            // Use floor for corridors to make them clearly man-made paths
            if (IsValidPosition(current, size)) tileIds[current.Y, current.X] = FloorTileId;
        }

        while (current.Y != to.Y)
        {
            current.Y += current.Y < to.Y ? 1 : -1;
            if (IsValidPosition(current, size)) tileIds[current.Y, current.X] = FloorTileId;
        }
    }

    private static bool IsValidPosition(Vector2I pos, Vector2I size)
    {
        return pos.X >= 0 && pos.X < size.X && pos.Y >= 0 && pos.Y < size.Y;
    }

    private static bool IsPassableTile(string tileId)
    {
        return tileId is FloorTileId or GrassTileId or DirtTileId;
    }

    /// <summary>
    /// Select a passable tile type using spatial hashing for terrain coherence.
    /// Uses position and signature to create coherent terrain patches.
    /// </summary>
    private string SelectPassableTile(Vector2I position, Vector2I mapSize, CardSignature signature)
    {
        // Generate spatial noise value based on position
        // Use low-frequency to create coherent patches (divide position by scale factor)
        var scale = 4f; // Larger = bigger terrain patches
        var nx = position.X / scale;
        var ny = position.Y / scale;

        // Simple spatial hash combining position with signature influence
        // Febris (temperature) biases grass vs dirt, Ordinem biases floor vs natural
        var spatialValue = GetSpatialNoise(nx, ny, signature);

        // Use signature to determine terrain bias:
        // - Positive Febris (hot) -> more dirt
        // - Negative Febris (cold) -> more grass
        // - High Ordinem (order) -> more floor tiles
        var febrisBias = signature[1]; // Febris = temperature
        var ordinemBias = signature[2]; // Ordinem = orderedness

        // Calculate thresholds based on signature
        var floorThreshold = 0.33f + ordinemBias * 0.2f; // More order = more floor
        var grassThreshold = floorThreshold + 0.33f - febrisBias * 0.15f; // Hot = less grass

        if (spatialValue < floorThreshold)
            return FloorTileId;
        if (spatialValue < grassThreshold)
            return GrassTileId;
        return DirtTileId;
    }

    /// <summary>
    /// Select a blocked tile type using spatial hashing.
    /// Uses position and signature to vary between wall and stone.
    /// </summary>
    private string SelectBlockedTile(Vector2I position, Vector2I mapSize, CardSignature signature)
    {
        var scale = 5f;
        var nx = position.X / scale;
        var ny = position.Y / scale;

        var spatialValue = GetSpatialNoise(nx, ny, signature);

        // Solidum (solidity) biases toward stone
        var solidumBias = signature[0];
        var stoneThreshold = 0.3f + solidumBias * 0.25f;

        // Add occasional water in low areas (negative Inertiae = heavy/low)
        var inertiaBias = signature[5]; // Inertiae = density
        if (inertiaBias < -0.3f && spatialValue > 0.85f)
            return WaterTileId;

        return spatialValue < stoneThreshold ? StoneTileId : WallTileId;
    }

    /// <summary>
    /// Generate spatial noise value [0, 1] for terrain coherence.
    /// Uses a simple but effective combination of sin waves for smooth variation.
    /// </summary>
    private float GetSpatialNoise(float x, float y, CardSignature signature)
    {
        // Combine multiple frequencies for varied but coherent noise
        // Use signature element to offset the noise pattern for variety
        var offset = (signature[4] + 1f) * 100f; // Varias for spatial offset

        var noise1 = Mathf.Sin(x * 0.7f + offset) * Mathf.Cos(y * 0.7f + offset);
        var noise2 = Mathf.Sin(x * 1.3f + y * 0.9f + offset * 0.5f);
        var noise3 = Mathf.Cos(x * 0.5f - y * 1.1f + offset * 0.3f);

        // Combine and normalize to [0, 1]
        var combined = (noise1 + noise2 + noise3) / 3f;
        return (combined + 1f) * 0.5f;
    }
}
