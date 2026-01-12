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

        var openSet = new PriorityQueue<int, float>();
        var cameFrom = new Dictionary<int, int>();
        var gScore = new Dictionary<int, float>();
        var inOpenSet = new HashSet<int>();

        gScore[startCell] = 0;
        var fScore = Heuristic(startCell, goalCell);
        openSet.Enqueue(startCell, fScore);
        inOpenSet.Add(startCell);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            inOpenSet.Remove(current);

            if (current == goalCell)
                return ReconstructPath(cameFrom, current);

            foreach (var neighbor in _mapData.GetAdjacentCells(current))
            {
                if (!_mapData.IsValidCell(neighbor))
                    continue;

                var movementCost = _mapData.GetMovementCost(current, neighbor);
                if (float.IsPositiveInfinity(movementCost))
                    continue;

                var tentativeGScore = gScore[current] + movementCost;

                if (!gScore.TryGetValue(neighbor, out var existingGScore) || tentativeGScore < existingGScore)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    var neighborFScore = tentativeGScore + Heuristic(neighbor, goalCell);

                    if (!inOpenSet.Contains(neighbor))
                    {
                        openSet.Enqueue(neighbor, neighborFScore);
                        inOpenSet.Add(neighbor);
                    }
                }
            }
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

    private static List<int> ReconstructPath(Dictionary<int, int> cameFrom, int current)
    {
        var path = new List<int> { current };

        while (cameFrom.TryGetValue(current, out var previous))
        {
            current = previous;
            path.Insert(0, current);
        }

        return path;
    }
}
