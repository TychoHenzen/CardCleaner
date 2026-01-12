using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Represents a connected region of unvisited cells.
/// </summary>
public readonly struct UnvisitedBlob
{
    public int EntryCellId { get; init; }
    public int Size { get; init; }
    public int WalkingDistance { get; init; }
}

/// <summary>
/// Exploration behavior that finds and paths to the nearest unexplored cell
/// on the frontier of the visited area.
/// Now works with any IMapData implementation (regular or irregular grids).
/// </summary>
public class FrontierExplorationBehavior
{
    private readonly IMapData _mapData;
    private readonly IVisibilityChecker _visibilityChecker;
    private readonly HashSet<int> _seenCells = new();
    private readonly HashSet<int> _visitedCells = new();
    private readonly HashSet<int> _currentlyVisibleCells = new();
    private readonly int _visionRange;

    /// <summary>
    /// Minimum blob size to be considered "significant" for prioritization.
    /// Smaller blobs are only targeted when no significant blobs remain.
    /// </summary>
    public int SignificantBlobThreshold { get; set; } = 5;

    public IReadOnlySet<int> SeenCells => _seenCells;
    public IReadOnlySet<int> VisitedCells => _visitedCells;

    /// <summary>
    /// Cells currently visible from the player's current position.
    /// This set is recalculated each time UpdateVision is called.
    /// </summary>
    public IReadOnlySet<int> CurrentlyVisibleCells => _currentlyVisibleCells;

    public FrontierExplorationBehavior(IMapData mapData, IVisibilityChecker visibilityChecker, int visionRange = 5)
    {
        _mapData = mapData;
        _visibilityChecker = visibilityChecker;
        _visionRange = visionRange;
    }

    /// <summary>
    /// Update seen and visited cells based on current position.
    /// Cells that are "trivially visible" (we can see them and all their neighbors,
    /// and they're connected to visited cells) are marked as visited without walking there.
    /// </summary>
    public void UpdateVision(int currentCellId)
    {
        try
        {
            // Clear currently visible cells - will be recalculated this frame
            _currentlyVisibleCells.Clear();

            // Only add passable cells to visited set
            if (_mapData.IsPassable(currentCellId))
                _visitedCells.Add(currentCellId);
            _seenCells.Add(currentCellId);
            _currentlyVisibleCells.Add(currentCellId);

            // Get current world position for distance calculations
            var currentPos = _mapData.GetCellCenter(currentCellId);

            // Check visibility to all cells within range
            var cellsInRange = _mapData.GetCellsInRadius(currentPos, _visionRange * EstimateCellSize());

            foreach (var targetCellId in cellsInRange)
            {
                if (targetCellId == currentCellId)
                    continue;

                var targetPos = _mapData.GetCellCenter(targetCellId);
                var distance = currentPos.DistanceTo(targetPos);

                // Skip if outside vision range (circular check)
                if (distance > _visionRange * EstimateCellSize())
                    continue;

                // Check line of sight
                if (_visibilityChecker.CanSee(currentCellId, targetCellId, _mapData))
                {
                    _seenCells.Add(targetCellId);
                    _currentlyVisibleCells.Add(targetCellId);
                }
            }

            // Mark trivially visible cells as visited
            MarkTriviallyVisibleCells();
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception in UpdateVision at cell {currentCellId}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Estimate average cell size for vision range calculation.
    /// </summary>
    private float EstimateCellSize()
    {
        if (_mapData.CellCount == 0)
            return 16f; // Default fallback

        // Sample first cell to estimate size
        var sampleArea = _mapData.GetCellArea(0);
        return Mathf.Sqrt(sampleArea);
    }

    /// <summary>
    /// Mark cells as visited if they are trivially visible (no need to walk there).
    /// Iterates until no more cells can be marked.
    /// </summary>
    private void MarkTriviallyVisibleCells()
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
                    ILog.Error($"MarkTriviallyVisibleCells exceeded {maxIterations} iterations - aborting");
                    break;
                }

                // Create a snapshot to avoid iteration issues
                var cellsToCheck = _seenCells.ToList();

                foreach (var cellId in cellsToCheck)
                {
                    if (_visitedCells.Contains(cellId))
                        continue;

                    if (!_mapData.IsPassable(cellId))
                        continue;

                    if (IsTriviallyVisible(cellId))
                    {
                        _visitedCells.Add(cellId);
                        changed = true;
                    }
                }
            } while (changed);
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception in MarkTriviallyVisibleCells (iteration {iteration}): {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Check if a cell is trivially visible - we can see it, all its passable neighbors
    /// have also been seen, and it's connected to the visited area.
    /// </summary>
    private bool IsTriviallyVisible(int cellId)
    {
        // Must be adjacent to at least one visited cell
        var hasVisitedNeighbor = false;
        foreach (var neighbor in _mapData.GetAdjacentCells(cellId))
        {
            if (_visitedCells.Contains(neighbor))
            {
                hasVisitedNeighbor = true;
                break;
            }
        }

        if (!hasVisitedNeighbor)
            return false;

        // All passable neighbors must be seen
        foreach (var neighbor in _mapData.GetAdjacentCells(cellId))
        {
            if (!_mapData.IsValidCell(neighbor))
                continue;

            // If neighbor is passable, it must be seen
            if (_mapData.IsPassable(neighbor) && !_seenCells.Contains(neighbor))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Find the best unvisited cell to explore using blob-based prioritization.
    /// Phase 1: Prioritize large blobs (size >= SignificantBlobThreshold) by score.
    /// Phase 2: When no significant blobs remain, target any remaining cell by distance.
    /// </summary>
    public int? FindNearestFrontierCell(int currentCellId)
    {
        var blobs = FindUnvisitedBlobs(currentCellId);

        if (blobs.Count == 0)
            return null;

        // Phase 1: Look for significant blobs (size >= threshold)
        var significantBlobs = blobs.Where(b => b.Size >= SignificantBlobThreshold).ToList();

        if (significantBlobs.Count > 0)
        {
            // Pick the closest significant blob, using size as tiebreaker
            var bestBlob = significantBlobs.OrderBy(b => b.WalkingDistance).ThenByDescending(b => b.Size).First();
            return bestBlob.EntryCellId;
        }

        // Phase 2: No significant blobs - fall back to nearest cell
        var nearestBlob = blobs.OrderBy(b => b.WalkingDistance).First();
        return nearestBlob.EntryCellId;
    }

    /// <summary>
    /// Find all passable cells that are adjacent to visited cells but not yet visited.
    /// </summary>
    public List<int> FindFrontierCells()
    {
        var frontier = new HashSet<int>();

        foreach (var visitedCell in _visitedCells)
        {
            foreach (var neighbor in _mapData.GetAdjacentCells(visitedCell))
            {
                if (_visitedCells.Contains(neighbor))
                    continue;

                if (!_mapData.IsValidCell(neighbor))
                    continue;

                if (_mapData.IsPassable(neighbor))
                    frontier.Add(neighbor);
            }
        }

        return frontier.ToList();
    }

    /// <summary>
    /// Check if all passable cells have been visited.
    /// </summary>
    public bool IsFullyExplored()
    {
        for (var i = 0; i < _mapData.CellCount; i++)
        {
            if (_mapData.IsPassable(i) && !_visitedCells.Contains(i))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Find all connected blobs of unvisited cells, with entry points and sizes.
    /// Uses BFS from current position to find walking distance to each blob.
    /// </summary>
    public List<UnvisitedBlob> FindUnvisitedBlobs(int currentCellId)
    {
        var blobs = new List<UnvisitedBlob>();
        var bfsVisited = new HashSet<int> { currentCellId };
        var blobAssigned = new HashSet<int>();
        var queue = new Queue<(int cellId, int distance)>();
        queue.Enqueue((currentCellId, 0));

        // BFS to find all reachable frontier cells with their walking distances
        var frontierWithDistance = new List<(int cellId, int distance)>();

        while (queue.Count > 0)
        {
            var (current, distance) = queue.Dequeue();

            foreach (var neighbor in _mapData.GetAdjacentCells(current))
            {
                if (bfsVisited.Contains(neighbor))
                    continue;

                if (!_mapData.IsValidCell(neighbor))
                    continue;

                if (!_mapData.IsPassable(neighbor))
                    continue;

                bfsVisited.Add(neighbor);

                if (!_visitedCells.Contains(neighbor))
                {
                    // Found an unvisited cell - record as potential blob entry
                    frontierWithDistance.Add((neighbor, distance + 1));
                }
                else
                {
                    // Visited cell - continue BFS
                    queue.Enqueue((neighbor, distance + 1));
                }
            }
        }

        // For each frontier cell, flood fill to find the connected blob size
        foreach (var (entryCell, walkingDistance) in frontierWithDistance)
        {
            if (blobAssigned.Contains(entryCell))
                continue;

            // Flood fill to find all connected unvisited cells
            var blobSize = 0;
            var floodQueue = new Queue<int>();
            floodQueue.Enqueue(entryCell);
            blobAssigned.Add(entryCell);

            while (floodQueue.Count > 0)
            {
                var cell = floodQueue.Dequeue();
                blobSize++;

                foreach (var neighbor in _mapData.GetAdjacentCells(cell))
                {
                    if (blobAssigned.Contains(neighbor))
                        continue;

                    if (!_mapData.IsValidCell(neighbor))
                        continue;

                    if (!_mapData.IsPassable(neighbor))
                        continue;

                    if (_visitedCells.Contains(neighbor))
                        continue;

                    blobAssigned.Add(neighbor);
                    floodQueue.Enqueue(neighbor);
                }
            }

            blobs.Add(new UnvisitedBlob
            {
                EntryCellId = entryCell,
                Size = blobSize,
                WalkingDistance = walkingDistance
            });
        }

        return blobs;
    }
}
