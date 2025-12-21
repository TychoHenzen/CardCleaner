using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Creates a simple connected map of passable/blocked tiles
/// </summary>
public class SimpleMapGenerator
{
    private readonly RandomNumberGenerator _rng;

    public SimpleMapGenerator(RandomNumberGenerator rng)
    {
        ArgumentNullException.ThrowIfNull(rng);
        _rng = rng;
    }

    public SimpleMapData GenerateMap(Vector2I size, CardSignature mapSeed, float blockedPercentage = 0.3f)
    {
        ILog.Print($"Generating simple map {size.X}x{size.Y} with {blockedPercentage:P0} blocked tiles");

        // Create initial random grid
        var grid = new bool[size.Y, size.X];
        var passableTiles = new List<Vector2I>();

        // First pass: randomly place blocked tiles
        for (var y = 0; y < size.Y; y++)
        for (var x = 0; x < size.X; x++)
        {
            var isBlocked = _rng.Randf() < blockedPercentage;
            grid[y, x] = !isBlocked; // true = passable, false = blocked

            if (!isBlocked) passableTiles.Add(new Vector2I(x, y));
        }

        // Ensure we have at least some passable tiles
        if (passableTiles.Count == 0)
        {
            // Force center tile to be passable
            var center = new Vector2I(size.X / 2, size.Y / 2);
            grid[center.Y, center.X] = true;
            passableTiles.Add(center);
        }

        // Ensure all passable tiles are connected
        EnsureConnectivity(grid, size, passableTiles);

        // Choose random positions for player and enemies
        var shuffledTiles = passableTiles.OrderBy(t => _rng.Randf()).ToList();
        var playerStart = shuffledTiles[0];

        // Place 1-3 enemies randomly
        var enemyCount = _rng.RandiRange(1, Mathf.Min(3, shuffledTiles.Count - 1));
        var enemyPositions = shuffledTiles.Skip(1).Take(enemyCount).ToList();

        ILog.Print(
            $"Map generated: {passableTiles.Count} passable tiles, player at {playerStart}, {enemyCount} enemies");

        return new SimpleMapData
        {
            Grid = grid,
            Size = size,
            PlayerStart = playerStart,
            EnemyPositions = enemyPositions,
            PassableTiles = passableTiles
        };
    }

    private void EnsureConnectivity(bool[,] grid, Vector2I size, List<Vector2I> passableTiles)
    {
        if (passableTiles.Count <= 1) return;

        // Find all connected components using flood fill
        var visited = new bool[size.Y, size.X];
        var components = new List<List<Vector2I>>();

        foreach (var tile in passableTiles)
        {
            if (visited[tile.Y, tile.X]) continue;

            var component = new List<Vector2I>();
            FloodFill(grid, visited, size, tile, component);
            if (component.Count > 0) components.Add(component);
        }

        // If we have multiple components, connect them
        while (components.Count > 1)
        {
            var component1 = components[0];
            var component2 = components[1];

            // Find closest pair of tiles between components
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

            // Create corridor between closest tiles
            CreateCorridor(grid, size, closest1, closest2);

            // Merge components
            component1.AddRange(component2);
            components.RemoveAt(1);

            // Update passable tiles list
            passableTiles.Clear();
            for (var y = 0; y < size.Y; y++)
            for (var x = 0; x < size.X; x++)
                if (grid[y, x])
                    passableTiles.Add(new Vector2I(x, y));
        }

        ILog.Print($"Connectivity ensured: {components.Count} connected component(s)");
    }

    private void FloodFill(bool[,] grid, bool[,] visited, Vector2I size, Vector2I start, List<Vector2I> component)
    {
        var stack = new Stack<Vector2I>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();

            if (current.X < 0 || current.X >= size.X ||
                current.Y < 0 || current.Y >= size.Y ||
                visited[current.Y, current.X] ||
                !grid[current.Y, current.X])
                continue;

            visited[current.Y, current.X] = true;
            component.Add(current);

            // Check 4-directional neighbors
            stack.Push(new Vector2I(current.X + 1, current.Y));
            stack.Push(new Vector2I(current.X - 1, current.Y));
            stack.Push(new Vector2I(current.X, current.Y + 1));
            stack.Push(new Vector2I(current.X, current.Y - 1));
        }
    }

    private void CreateCorridor(bool[,] grid, Vector2I size, Vector2I from, Vector2I to)
    {
        // Simple L-shaped corridor
        var current = from;

        // Move horizontally first
        while (current.X != to.X)
        {
            current.X += current.X < to.X ? 1 : -1;
            if (IsValidPosition(current, size)) grid[current.Y, current.X] = true;
        }

        // Then move vertically
        while (current.Y != to.Y)
        {
            current.Y += current.Y < to.Y ? 1 : -1;
            if (IsValidPosition(current, size)) grid[current.Y, current.X] = true;
        }
    }

    private static bool IsValidPosition(Vector2I pos, Vector2I size)
    {
        return pos.X >= 0 && pos.X < size.X && pos.Y >= 0 && pos.Y < size.Y;
    }
}