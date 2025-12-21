using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

// Using built-in PriorityQueue from System.Collections.Generic (.NET 6+)

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Simple AI that explores a map autonomously
/// </summary>
public class ExplorationAI
{
    private readonly SimpleMapData _mapData;
    private readonly HashSet<Vector2I> _visitedTiles = new();
    private readonly List<Vector2I> _pathToTarget = new();

    public Vector2I CurrentPosition { get; private set; }
    public bool HasFoundEnemy { get; private set; }
    public Vector2I EnemyPosition { get; private set; }
    public bool HasFinishedExploration => _visitedTiles.Count >= _mapData.PassableTiles.Count || HasFoundEnemy;

    public event Action<Vector2I> PlayerMoved;
    public event Action<Vector2I> EnemyEncountered;

    public ExplorationAI(SimpleMapData mapData)
    {
        ArgumentNullException.ThrowIfNull(mapData);
        ArgumentNullException.ThrowIfNull(mapData.PassableTiles);
        ArgumentNullException.ThrowIfNull(mapData.EnemyPositions);

        _mapData = mapData;
        CurrentPosition = mapData.PlayerStart;
        _visitedTiles.Add(CurrentPosition);

        ILog.Print($"Exploration AI initialized at {CurrentPosition}");
    }

    /// <summary>
    /// Perform one step of exploration. Returns true if exploration should continue.
    /// </summary>
    public bool StepExploration()
    {
        if (HasFinishedExploration) return false;

        // Check if we've encountered an enemy
        if (_mapData.EnemyPositions.Contains(CurrentPosition))
        {
            HasFoundEnemy = true;
            EnemyPosition = CurrentPosition;
            ILog.Print($"Enemy encountered at {CurrentPosition}!");
            EnemyEncountered?.Invoke(CurrentPosition);
            return false;
        }

        // If we have a path, follow it
        if (_pathToTarget.Count > 0)
        {
            var nextPosition = _pathToTarget[0];
            _pathToTarget.RemoveAt(0);
            MoveToPosition(nextPosition);
            return true;
        }

        // Find next target to explore
        var target = FindNextExplorationTarget();
        if (target == null)
        {
            ILog.Print("No more tiles to explore - exploration complete");
            return false;
        }

        // Calculate path to target
        _pathToTarget.Clear();
        var path = FindPath(CurrentPosition, target.Value);
        if (path.Count > 1)
        {
            _pathToTarget.AddRange(path.Skip(1)); // Skip current position
            return StepExploration(); // Take first step immediately
        }

        ILog.Print("No path to target found - exploration stuck");
        return false;
    }

    private void MoveToPosition(Vector2I newPosition)
    {
        if (!_mapData.IsPassable(newPosition))
        {
            ILog.Error($"Attempted to move to blocked position {newPosition}");
            return;
        }

        CurrentPosition = newPosition;
        _visitedTiles.Add(CurrentPosition);

        ILog.Print($"Player moved to {CurrentPosition} (visited {_visitedTiles.Count}/{_mapData.PassableTiles.Count})");
        PlayerMoved?.Invoke(CurrentPosition);
    }

    private Vector2I? FindNextExplorationTarget()
    {
        // Prioritize enemy positions if we haven't found one yet
        var unvisitedEnemies = _mapData.EnemyPositions.Where(e => !_visitedTiles.Contains(e)).ToList();
        if (unvisitedEnemies.Count > 0) return unvisitedEnemies.OrderBy(e => CurrentPosition.DistanceTo(e)).First();

        // Otherwise, find nearest unvisited passable tile
        var unvisitedTiles = _mapData.PassableTiles.Where(t => !_visitedTiles.Contains(t)).ToList();
        if (unvisitedTiles.Count == 0) return null;

        return unvisitedTiles.OrderBy(t => CurrentPosition.DistanceTo(t)).First();
    }

    /// <summary>
    /// Simple A* pathfinding
    /// </summary>
    private List<Vector2I> FindPath(Vector2I start, Vector2I goal)
    {
        var openSet = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float>();
        var fScore = new Dictionary<Vector2I, float>();

        gScore[start] = 0;
        fScore[start] = Heuristic(start, goal);
        openSet.Enqueue(start, fScore[start]);

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current == goal) return ReconstructPath(cameFrom, current);

            foreach (var neighbor in GetNeighbors(current))
            {
                if (!_mapData.IsPassable(neighbor)) continue;

                var tentativeGScore = gScore[current] + 1; // All moves cost 1

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goal);

                    openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        return new List<Vector2I>(); // No path found
    }

    private static float Heuristic(Vector2I a, Vector2I b)
    {
        return Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y); // Manhattan distance
    }

    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> cameFrom, Vector2I current)
    {
        var path = new List<Vector2I> { current };

        while (cameFrom.ContainsKey(current))
        {
            current = cameFrom[current];
            path.Insert(0, current);
        }

        return path;
    }

    private IEnumerable<Vector2I> GetNeighbors(Vector2I pos)
    {
        yield return new Vector2I(pos.X + 1, pos.Y);
        yield return new Vector2I(pos.X - 1, pos.Y);
        yield return new Vector2I(pos.X, pos.Y + 1);
        yield return new Vector2I(pos.X, pos.Y - 1);
    }
}