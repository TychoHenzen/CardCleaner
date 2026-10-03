namespace CardCleaner.Scripts.Core.Services;

using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

/// <summary>
/// Generic A* pathfinder that works with any IMapData implementation.
/// Operates on cell IDs rather than Vector2I positions.
/// </summary>
public class Pathfinder
{
    private readonly IMapData _mapData;

    public Pathfinder(IMapData mapData)
    {
        _mapData = mapData;
    }

    /// <summary>
    /// Find a path from startCell to goalCell using A*.
    /// Returns empty list if no path exists.
    /// </summary>
    public List<int> FindPath(int startCell, int goalCell)
    {
        if (!_mapData.IsValidCell(startCell) || !_mapData.IsValidCell(goalCell))
            return new List<int>();

        if (startCell == goalCell)
            return new List<int> { startCell };

        var search = new AStarSearchState(startCell, Heuristic(startCell, goalCell));

        while (search.TryDequeue(out var current))
        {
            if (current == goalCell)
                return search.ReconstructPath(current);

            ExpandNeighbors(search, current, goalCell);
        }

        return new List<int>(); // No path found
    }

    /// <summary>
    /// Find a path and convert to world positions.
    /// </summary>
    public List<Vector2> FindPathAsWorldPositions(int startCell, int goalCell)
    {
        var cellPath = FindPath(startCell, goalCell);
        var worldPath = new List<Vector2>(cellPath.Count);

        foreach (var cellId in cellPath)
        {
            worldPath.Add(_mapData.GetCellCenter(cellId));
        }

        return worldPath;
    }

    /// <summary>
    /// Find a path from a world position to a target world position.
    /// </summary>
    public List<Vector2> FindPathBetweenPositions(Vector2 start, Vector2 goal)
    {
        var startCell = _mapData.GetCellAtPosition(start);
        var goalCell = _mapData.GetCellAtPosition(goal);

        if (!startCell.HasValue || !goalCell.HasValue)
            return new List<Vector2>();

        return FindPathAsWorldPositions(startCell.Value, goalCell.Value);
    }

    /// <summary>
    /// Calculate Euclidean distance heuristic between two cells.
    /// Works for both regular and irregular grids.
    /// </summary>
    private float Heuristic(int cellA, int cellB)
    {
        var posA = _mapData.GetCellCenter(cellA);
        var posB = _mapData.GetCellCenter(cellB);
        return posA.DistanceTo(posB);
    }

    private void ExpandNeighbors(AStarSearchState search, int current, int goalCell)
    {
        foreach (var neighbor in _mapData.GetAdjacentCells(current))
        {
            if (!_mapData.IsValidCell(neighbor))
                continue;

            var movementCost = _mapData.GetMovementCost(current, neighbor);
            if (float.IsPositiveInfinity(movementCost))
                continue;

            if (!search.TryImprovePath(current, neighbor, movementCost, out var newCost))
                continue;

            search.EnqueueIfAbsent(neighbor, newCost + Heuristic(neighbor, goalCell));
        }
    }
}
