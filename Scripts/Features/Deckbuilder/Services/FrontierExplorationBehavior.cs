using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Exploration behavior that finds and paths to the nearest unexplored tile
/// on the frontier of the visited area.
/// </summary>
public class FrontierExplorationBehavior
{
    private readonly SimpleMapData _mapData;
    private readonly IVisibilityChecker _visibilityChecker;
    private readonly HashSet<Vector2I> _seenTiles = new();
    private readonly HashSet<Vector2I> _visitedTiles = new();
    private readonly int _visionRange;

    public IReadOnlySet<Vector2I> SeenTiles => _seenTiles;
    public IReadOnlySet<Vector2I> VisitedTiles => _visitedTiles;

    public FrontierExplorationBehavior(SimpleMapData mapData, IVisibilityChecker visibilityChecker, int visionRange = 5)
    {
        _mapData = mapData;
        _visibilityChecker = visibilityChecker;
        _visionRange = visionRange;
    }

    /// <summary>
    /// Update seen and visited tiles based on current position.
    /// Tiles that are "trivially visible" (we can see them and all their neighbors,
    /// and they're connected to visited tiles) are marked as visited without walking there.
    /// </summary>
    public void UpdateVision(Vector2I currentPosition)
    {
        try
        {
            _visitedTiles.Add(currentPosition);
            _seenTiles.Add(currentPosition);

            // First pass: update seen tiles
            for (var dy = -_visionRange; dy <= _visionRange; dy++)
            for (var dx = -_visionRange; dx <= _visionRange; dx++)
            {
                var targetPos = new Vector2I(currentPosition.X + dx, currentPosition.Y + dy);

                // Skip if out of bounds
                if (!IsInBounds(targetPos))
                    continue;

                // Skip if already seen
                if (_seenTiles.Contains(targetPos))
                    continue;

                // Check if within vision range (circular)
                if (dx * dx + dy * dy > _visionRange * _visionRange)
                    continue;

                // Check line of sight
                if (_visibilityChecker.CanSee(currentPosition, targetPos, _mapData))
                    _seenTiles.Add(targetPos);
            }

            // Second pass: mark trivially visible tiles as visited
            MarkTriviallyVisibleTiles();
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception in UpdateVision at {currentPosition}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Mark tiles as visited if they are trivially visible (no need to walk there).
    /// Iterates until no more tiles can be marked.
    /// </summary>
    private void MarkTriviallyVisibleTiles()
    {
        const int maxIterations = 1000; // Safety limit
        var iteration = 0;
        bool changed;

        try
        {
            do
            {
                changed = false;
                iteration++;

                if (iteration > maxIterations)
                {
                    ILog.Error($"MarkTriviallyVisibleTiles exceeded {maxIterations} iterations - aborting");
                    break;
                }

                // Create a snapshot to avoid potential iteration issues
                var tilesToCheck = _seenTiles.ToList();

                foreach (var tile in tilesToCheck)
                {
                    if (_visitedTiles.Contains(tile))
                        continue;

                    if (!_mapData.IsPassable(tile))
                        continue;

                    if (IsTriviallyVisible(tile))
                    {
                        _visitedTiles.Add(tile);
                        changed = true;
                    }
                }
            } while (changed);
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception in MarkTriviallyVisibleTiles (iteration {iteration}): {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Check if a tile is trivially visible - we can see it and all its neighbors,
    /// and it's connected to the visited area.
    /// </summary>
    private bool IsTriviallyVisible(Vector2I tile)
    {
        // Must be adjacent to at least one visited tile
        var hasVisitedNeighbor = false;
        foreach (var neighbor in GetNeighbors(tile))
        {
            if (_visitedTiles.Contains(neighbor))
            {
                hasVisitedNeighbor = true;
                break;
            }
        }

        if (!hasVisitedNeighbor)
            return false;

        // All passable neighbors must be seen
        foreach (var neighbor in GetNeighbors(tile))
        {
            if (!IsInBounds(neighbor))
                continue;

            // If neighbor is passable, it must be seen
            if (_mapData.IsPassable(neighbor) && !_seenTiles.Contains(neighbor))
                return false;
        }

        return true;
    }

    private bool IsInBounds(Vector2I pos)
    {
        return pos.X >= 0 && pos.X < _mapData.Size.X &&
               pos.Y >= 0 && pos.Y < _mapData.Size.Y;
    }

    /// <summary>
    /// Find the nearest unvisited passable tile by actual walking distance using BFS.
    /// This prevents picking tiles that are "close" as the crow flies but require backtracking.
    /// Returns null if no frontier tiles exist.
    /// </summary>
    public Vector2I? FindNearestFrontierTile(Vector2I currentPosition)
    {
        // BFS to find the nearest unvisited passable tile
        var visited = new HashSet<Vector2I> { currentPosition };
        var queue = new Queue<Vector2I>();
        queue.Enqueue(currentPosition);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            foreach (var neighbor in GetNeighbors(current))
            {
                if (visited.Contains(neighbor))
                    continue;

                if (!IsInBounds(neighbor))
                    continue;

                if (!_mapData.IsPassable(neighbor))
                    continue;

                visited.Add(neighbor);

                // If this tile hasn't been visited by the explorer, it's a frontier tile
                if (!_visitedTiles.Contains(neighbor))
                    return neighbor;

                // Otherwise, continue searching from this tile
                queue.Enqueue(neighbor);
            }
        }

        return null; // No frontier tiles reachable
    }

    /// <summary>
    /// Find all passable tiles that are adjacent to visited tiles but not yet visited.
    /// </summary>
    public List<Vector2I> FindFrontierTiles()
    {
        var frontier = new HashSet<Vector2I>();

        foreach (var visitedTile in _visitedTiles)
        {
            foreach (var neighbor in GetNeighbors(visitedTile))
            {
                if (_visitedTiles.Contains(neighbor))
                    continue;

                if (!IsInBounds(neighbor))
                    continue;

                if (_mapData.IsPassable(neighbor))
                    frontier.Add(neighbor);
            }
        }

        return frontier.ToList();
    }

    /// <summary>
    /// Check if all passable tiles have been visited.
    /// </summary>
    public bool IsFullyExplored()
    {
        return _mapData.PassableTiles.All(t => _visitedTiles.Contains(t));
    }

    private IEnumerable<Vector2I> GetNeighbors(Vector2I pos)
    {
        yield return new Vector2I(pos.X + 1, pos.Y);
        yield return new Vector2I(pos.X - 1, pos.Y);
        yield return new Vector2I(pos.X, pos.Y + 1);
        yield return new Vector2I(pos.X, pos.Y - 1);
    }
}
