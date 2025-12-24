using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

public enum ExplorationMode
{
    FrontierExploration,
    PathToEnemy
}

/// <summary>
/// AI that explores a map using frontier-based exploration,
/// switching to enemy targeting when enemies become visible.
/// </summary>
public class ExplorationAI
{
    private readonly SimpleMapData _mapData;
    private readonly HashSet<Vector2I> _visitedTiles = new();
    private readonly List<Vector2I> _pathToTarget = new();
    private readonly FrontierExplorationBehavior _frontierBehavior;
    private readonly IVisibilityChecker _visibilityChecker;
    private Vector2I? _currentTargetTile;

    public Vector2I CurrentPosition { get; private set; }
    public bool HasFoundEnemy { get; private set; }
    public Vector2I? VisibleEnemyPosition { get; private set; }
    public ExplorationMode CurrentMode { get; private set; } = ExplorationMode.FrontierExploration;

    public bool HasFinishedExploration => _frontierBehavior.IsFullyExplored() || HasFoundEnemy;
    public IReadOnlySet<Vector2I> SeenTiles => _frontierBehavior.SeenTiles;

    /// <summary>
    /// The current path being followed (for debug visualization).
    /// </summary>
    public IReadOnlyList<Vector2I> CurrentPath => _pathToTarget;

    /// <summary>
    /// The current target tile the agent is trying to reach (for debug visualization).
    /// </summary>
    public Vector2I? CurrentTarget => _currentTargetTile;

    public event Action<Vector2I>? PlayerMoved;
    public event Action<Vector2I>? EnemyEncountered;
    public event Action<Vector2I>? EnemySpotted;
    public event Action<IReadOnlySet<Vector2I>>? VisitedTilesUpdated;
    public event Action? PathUpdated;

    public ExplorationAI(SimpleMapData mapData, Vector2I? startPosition = null, IVisibilityChecker? visibilityChecker = null, int visionRange = 5)
    {
        ArgumentNullException.ThrowIfNull(mapData);
        ArgumentNullException.ThrowIfNull(mapData.PassableTiles);
        ArgumentNullException.ThrowIfNull(mapData.EnemyPositions);

        _mapData = mapData;
        _visibilityChecker = visibilityChecker ?? (ServiceLocator.Has<IVisibilityChecker>()
            ? ServiceLocator.Get<IVisibilityChecker>()
            : new SimpleVisibilityChecker());

        _frontierBehavior = new FrontierExplorationBehavior(mapData, _visibilityChecker, visionRange);

        CurrentPosition = startPosition ?? mapData.PlayerStart;
        _visitedTiles.Add(CurrentPosition);

        // Initial vision update
        try
        {
            _frontierBehavior.UpdateVision(CurrentPosition);
            VisitedTilesUpdated?.Invoke(_frontierBehavior.VisitedTiles);
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception during initial vision update: {ex.Message}\n{ex.StackTrace}");
        }

        ILog.Print($"Exploration AI initialized at {CurrentPosition}");
    }

    /// <summary>
    /// Perform one step of exploration. Returns true if exploration should continue.
    /// </summary>
    public bool StepExploration()
    {
        try
        {
            if (HasFinishedExploration) return false;

            // Check if we've physically reached an enemy position
            if (_mapData.EnemyPositions.Contains(CurrentPosition))
            {
                HasFoundEnemy = true;
                VisibleEnemyPosition = CurrentPosition;
                ILog.Print($"Enemy encountered at {CurrentPosition}!");
                EnemyEncountered?.Invoke(CurrentPosition);
                return false;
            }

            // Check for visible enemies and update mode
            CheckForVisibleEnemies();

            // If we have a path, follow it - but first check if target is still valid
            if (_pathToTarget.Count > 0)
            {
                // If target became visited (e.g., marked trivially visible), recalculate
                if (_currentTargetTile.HasValue &&
                    CurrentMode == ExplorationMode.FrontierExploration &&
                    _frontierBehavior.VisitedTiles.Contains(_currentTargetTile.Value))
                {
                    ILog.Print($"Target {_currentTargetTile.Value} became visited - recalculating path");
                    _pathToTarget.Clear();
                    _currentTargetTile = null;
                    PathUpdated?.Invoke();
                    // Fall through to find new target
                }
                else
                {
                    var nextPosition = _pathToTarget[0];
                    _pathToTarget.RemoveAt(0);
                    MoveToPosition(nextPosition);
                    return true;
                }
            }

            // Find next target based on current mode
            Vector2I? target = CurrentMode switch
            {
                ExplorationMode.PathToEnemy => VisibleEnemyPosition,
                ExplorationMode.FrontierExploration => _frontierBehavior.FindNearestFrontierTile(CurrentPosition),
                _ => null
            };

            if (target == null)
            {
                _currentTargetTile = null;
                PathUpdated?.Invoke();
                ILog.Print("No more targets to explore - exploration complete");
                return false;
            }

            // Calculate path to target
            _pathToTarget.Clear();
            _currentTargetTile = target.Value;
            var path = FindPath(CurrentPosition, target.Value);
            if (path.Count > 1)
            {
                // Take the first step now (don't use recursion to avoid stack issues)
                _pathToTarget.AddRange(path.Skip(1)); // Skip current position
                PathUpdated?.Invoke();
                var nextPosition = _pathToTarget[0];
                _pathToTarget.RemoveAt(0);
                MoveToPosition(nextPosition);
                return true;
            }

            _currentTargetTile = null;
            PathUpdated?.Invoke();
            ILog.Print($"No path to target {target.Value} found from {CurrentPosition} - exploration stuck");
            return false;
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception in StepExploration: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    private void CheckForVisibleEnemies()
    {
        Vector2I? closestVisibleEnemy = null;
        var closestDistance = float.MaxValue;

        foreach (var enemyPos in _mapData.EnemyPositions)
        {
            if (_visibilityChecker.CanSee(CurrentPosition, enemyPos, _mapData))
            {
                var distance = CurrentPosition.DistanceTo(enemyPos);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestVisibleEnemy = enemyPos;
                }
            }
        }

        if (closestVisibleEnemy != null)
        {
            if (CurrentMode != ExplorationMode.PathToEnemy)
            {
                ILog.Print($"Enemy spotted at {closestVisibleEnemy}! Switching to pursuit mode.");
                EnemySpotted?.Invoke(closestVisibleEnemy.Value);
            }
            CurrentMode = ExplorationMode.PathToEnemy;
            VisibleEnemyPosition = closestVisibleEnemy;
            _pathToTarget.Clear();
            _currentTargetTile = null;
            PathUpdated?.Invoke();
        }
        else
        {
            // No enemies visible - return to exploration
            if (CurrentMode == ExplorationMode.PathToEnemy)
            {
                ILog.Print("Enemy no longer visible. Returning to exploration.");
                _pathToTarget.Clear();
                _currentTargetTile = null;
                PathUpdated?.Invoke();
            }
            CurrentMode = ExplorationMode.FrontierExploration;
            VisibleEnemyPosition = null;
        }
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

        // Update vision from new position
        _frontierBehavior.UpdateVision(CurrentPosition);

        ILog.Print($"Player moved to {CurrentPosition} (seen {_frontierBehavior.SeenTiles.Count} tiles, visited {_frontierBehavior.VisitedTiles.Count})");
        PlayerMoved?.Invoke(CurrentPosition);
        VisitedTilesUpdated?.Invoke(_frontierBehavior.VisitedTiles);
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

                if (!gScore.TryGetValue(neighbor, out var existingGScore) || tentativeGScore < existingGScore)
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
