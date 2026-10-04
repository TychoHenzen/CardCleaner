using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services.FrontierExploration;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Exploration behavior that finds and paths to the nearest unexplored cell
/// on the frontier of the visited area.
/// Now works with any IMapData implementation (regular or irregular grids).
/// </summary>
public class FrontierExplorationBehavior
{
    private readonly IMapData _mapData;
    private readonly IFogOfWar? _fogOfWar;
    private readonly LocalVisionTracker _localVision;
    private readonly HashSet<int> _visitedCells = new();

    /// <summary>
    /// Minimum blob size to be considered "significant" for prioritization.
    /// Smaller blobs are only targeted when no significant blobs remain.
    /// </summary>
    public int SignificantBlobThreshold { get; set; } = 5;

    /// <summary>
    /// All cells that have been seen. Uses fog of war system if provided,
    /// otherwise uses local tracking.
    /// </summary>
    public IReadOnlySet<int> SeenCells => _fogOfWar?.SeenCells ?? _localVision.SeenCells;

    public IReadOnlySet<int> VisitedCells => _visitedCells;

    /// <summary>
    /// Cells currently visible from the player's current position.
    /// Uses fog of war system if provided, otherwise uses local tracking.
    /// </summary>
    public IReadOnlySet<int> CurrentlyVisibleCells =>
        _fogOfWar?.CurrentlyVisibleCells ?? _localVision.CurrentlyVisibleCells;

    public FrontierExplorationBehavior(
        IMapData mapData,
        IVisibilityChecker visibilityChecker,
        int visionRange = 5,
        IFogOfWar? fogOfWar = null)
    {
        _mapData = mapData;
        _localVision = new LocalVisionTracker(mapData, visibilityChecker, visionRange);
        _fogOfWar = fogOfWar;
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
            // Only add passable cells to visited set
            if (_mapData.IsPassable(currentCellId))
                _visitedCells.Add(currentCellId);

            // If using external fog of war, skip local visibility tracking
            // The fog system handles seen/visible cells
            if (_fogOfWar == null)
                _localVision.Update(currentCellId);

            // Mark trivially visible cells as visited (uses SeenCells property which
            // delegates to fog system if available)
            MarkTriviallyVisibleCells();
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception in UpdateVision at cell {currentCellId}: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
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
                // Use SeenCells property which delegates to fog system if available
                var cellsToCheck = SeenCells.ToList();

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
            ILog.Error(
                $"Exception in MarkTriviallyVisibleCells (iteration {iteration}): " +
                $"{ex.Message}\n{ex.StackTrace}");
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

        // All passable neighbors must be seen (uses SeenCells property)
        var seenCells = SeenCells;
        foreach (var neighbor in _mapData.GetAdjacentCells(cellId))
        {
            if (!_mapData.IsValidCell(neighbor))
                continue;

            // If neighbor is passable, it must be seen
            if (_mapData.IsPassable(neighbor) && !seenCells.Contains(neighbor))
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
    internal List<UnvisitedBlob> FindUnvisitedBlobs(int currentCellId)
    {
        return new UnvisitedBlobFinder(_mapData, _visitedCells).Find(currentCellId);
    }
}
