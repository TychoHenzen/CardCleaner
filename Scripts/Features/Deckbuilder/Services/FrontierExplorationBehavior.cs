using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Represents a connected region of unvisited tiles.
/// </summary>
public readonly struct UnvisitedBlob
{
    public Vector2I EntryPoint { get; init; }
    public int Size { get; init; }
    public int WalkingDistance { get; init; }
}

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
    private readonly HashSet<Vector2I> _currentlyVisibleTiles = new();
    private readonly int _visionRange;

    /// <summary>
    /// Minimum blob size to be considered "significant" for prioritization.
    /// Smaller blobs are only targeted when no significant blobs remain.
    /// </summary>
    public int SignificantBlobThreshold { get; set; } = 5;

    public IReadOnlySet<Vector2I> SeenTiles => _seenTiles;
    public IReadOnlySet<Vector2I> VisitedTiles => _visitedTiles;

    /// <summary>
    /// Tiles currently visible from the player's current position.
    /// This set is recalculated each time UpdateVision is called.
    /// </summary>
    public IReadOnlySet<Vector2I> CurrentlyVisibleTiles => _currentlyVisibleTiles;

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
            // Clear currently visible tiles - will be recalculated this frame
            _currentlyVisibleTiles.Clear();

            // Only add passable tiles to visited set
            if (_mapData.IsPassable(currentPosition))
                _visitedTiles.Add(currentPosition);
            _seenTiles.Add(currentPosition);
            _currentlyVisibleTiles.Add(currentPosition);

            // First pass: update seen and currently visible tiles
            for (var dy = -_visionRange; dy <= _visionRange; dy++)
            for (var dx = -_visionRange; dx <= _visionRange; dx++)
            {
                var targetPos = new Vector2I(currentPosition.X + dx, currentPosition.Y + dy);

                // Skip if out of bounds
                if (!IsInBounds(targetPos))
                    continue;

                // Check if within vision range (circular)
                if (dx * dx + dy * dy > _visionRange * _visionRange)
                    continue;

                // Check line of sight
                if (_visibilityChecker.CanSee(currentPosition, targetPos, _mapData))
                {
                    _seenTiles.Add(targetPos);
                    _currentlyVisibleTiles.Add(targetPos);
                }
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
    /// Find the best unvisited tile to explore using blob-based prioritization.
    /// Phase 1: Prioritize large blobs (size >= SignificantBlobThreshold) by score.
    /// Phase 2: When no significant blobs remain, target any remaining tile by distance.
    /// </summary>
    public Vector2I? FindNearestFrontierTile(Vector2I currentPosition)
    {
        var blobs = FindUnvisitedBlobs(currentPosition);

        if (blobs.Count == 0)
            return null;

        // Phase 1: Look for significant blobs (size >= threshold)
        var significantBlobs = blobs.Where(b => b.Size >= SignificantBlobThreshold).ToList();

        if (significantBlobs.Count > 0)
        {
            // Pick the closest significant blob, using size as tiebreaker
            var bestBlob = significantBlobs.OrderBy(b => b.WalkingDistance).ThenByDescending(b => b.Size).First();
            return bestBlob.EntryPoint;
        }

        // Phase 2: No significant blobs - fall back to nearest tile
        // This handles single-tile cleanup when major exploration is done
        var nearestBlob = blobs.OrderBy(b => b.WalkingDistance).First();
        return nearestBlob.EntryPoint;
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

    /// <summary>
    /// Find all connected blobs of unvisited tiles, with entry points and sizes.
    /// Uses BFS from current position to find walking distance to each blob.
    /// </summary>
    public List<UnvisitedBlob> FindUnvisitedBlobs(Vector2I currentPosition)
    {
        var blobs = new List<UnvisitedBlob>();
        var bfsVisited = new HashSet<Vector2I> { currentPosition };
        var blobAssigned = new HashSet<Vector2I>();
        var queue = new Queue<(Vector2I pos, int distance)>();
        queue.Enqueue((currentPosition, 0));

        // BFS to find all reachable frontier tiles with their walking distances
        var frontierWithDistance = new List<(Vector2I tile, int distance)>();

        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();

            foreach (var neighbor in GetNeighbors(current))
            {
                if (bfsVisited.Contains(neighbor))
                    continue;

                if (!IsInBounds(neighbor))
                    continue;

                if (!_mapData.IsPassable(neighbor))
                    continue;

                bfsVisited.Add(neighbor);

                if (!_visitedTiles.Contains(neighbor))
                {
                    // Found an unvisited tile - record as potential blob entry
                    frontierWithDistance.Add((neighbor, distance + 1));
                }
                else
                {
                    // Visited tile - continue BFS
                    queue.Enqueue((neighbor, distance + 1));
                }
            }
        }

        // For each frontier tile, flood fill to find the connected blob size
        foreach (var (entryPoint, walkingDistance) in frontierWithDistance)
        {
            if (blobAssigned.Contains(entryPoint))
                continue;

            // Flood fill to find all connected unvisited tiles
            var blobSize = 0;
            var floodQueue = new Queue<Vector2I>();
            floodQueue.Enqueue(entryPoint);
            blobAssigned.Add(entryPoint);

            while (floodQueue.Count > 0)
            {
                var tile = floodQueue.Dequeue();
                blobSize++;

                foreach (var neighbor in GetNeighbors(tile))
                {
                    if (blobAssigned.Contains(neighbor))
                        continue;

                    if (!IsInBounds(neighbor))
                        continue;

                    if (!_mapData.IsPassable(neighbor))
                        continue;

                    if (_visitedTiles.Contains(neighbor))
                        continue;

                    blobAssigned.Add(neighbor);
                    floodQueue.Enqueue(neighbor);
                }
            }

            blobs.Add(new UnvisitedBlob
            {
                EntryPoint = entryPoint,
                Size = blobSize,
                WalkingDistance = walkingDistance
            });
        }

        return blobs;
    }

    private IEnumerable<Vector2I> GetNeighbors(Vector2I pos)
    {
        yield return new Vector2I(pos.X + 1, pos.Y);
        yield return new Vector2I(pos.X - 1, pos.Y);
        yield return new Vector2I(pos.X, pos.Y + 1);
        yield return new Vector2I(pos.X, pos.Y - 1);
    }
}
