using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;
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
    private readonly IExplorationStrategy _frontierStrategy;
    private readonly IExplorationStrategy _enemyPursuitStrategy;
    private IExplorationStrategy _currentStrategy;
    private Vector2I? _currentTargetTile;
    private Vector2I? _lastKnownEnemyPosition;
    private Vector2I? _pendingEnemyPosition;

    public Vector2I CurrentPosition { get; private set; }
    public bool HasFoundEnemy { get; private set; }
    public Vector2I? VisibleEnemyPosition { get; private set; }
    public ExplorationMode CurrentMode { get; private set; } = ExplorationMode.FrontierExploration;

    public bool HasFinishedExploration =>
        HasFoundEnemy ||
        (_frontierBehavior.IsFullyExplored() &&
         CurrentMode != ExplorationMode.PathToEnemy &&
         _pendingEnemyPosition == null);
    public IReadOnlySet<Vector2I> SeenTiles => _frontierBehavior.SeenTiles;
    public IReadOnlySet<Vector2I> CurrentlyVisibleTiles => _frontierBehavior.CurrentlyVisibleTiles;

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
    public event Action<IReadOnlySet<Vector2I>, IReadOnlySet<Vector2I>>? VisibilityUpdated;
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

        // Initialize exploration strategies
        _frontierStrategy = new FrontierExplorationStrategy();
        _enemyPursuitStrategy = new PathToEnemyStrategy();
        _currentStrategy = _frontierStrategy;

        CurrentPosition = startPosition ?? mapData.PlayerStart;
        _visitedTiles.Add(CurrentPosition);

        // Initial vision update
        try
        {
            _frontierBehavior.UpdateVision(CurrentPosition);
            VisitedTilesUpdated?.Invoke(_frontierBehavior.VisitedTiles);
            VisibilityUpdated?.Invoke(_frontierBehavior.SeenTiles, _frontierBehavior.CurrentlyVisibleTiles);
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception during initial vision update: {ex.Message}\n{ex.StackTrace}");
        }

        ILog.Print($"Exploration AI initialized at {CurrentPosition}");
    }

    /// <summary>
    /// Set the exploration mode and corresponding strategy.
    /// </summary>
    private void SetMode(ExplorationMode mode)
    {
        CurrentMode = mode;
        _currentStrategy = mode switch
        {
            ExplorationMode.FrontierExploration => _frontierStrategy,
            ExplorationMode.PathToEnemy => _enemyPursuitStrategy,
            _ => _frontierStrategy
        };
    }

    /// <summary>
    /// Perform one step of exploration. Returns true if exploration should continue.
    /// </summary>
    public bool StepExploration()
    {
        try
        {
            // Check if we've physically reached an enemy position
            if (_mapData.EnemyPositions.Contains(CurrentPosition))
            {
                HasFoundEnemy = true;
                VisibleEnemyPosition = CurrentPosition;
                ILog.Print($"Enemy encountered at {CurrentPosition}!");
                EnemyEncountered?.Invoke(CurrentPosition);
                return false;
            }

            // Check for visible enemies BEFORE checking if exploration is complete
            // (enemies can be visible even if all tiles are trivially visited)
            CheckForVisibleEnemies();

            if (HasFinishedExploration) return false;

            // If we have a path, follow it to completion (commit to destination)
            if (_pathToTarget.Count > 0)
            {
                var nextPosition = _pathToTarget[0];
                _pathToTarget.RemoveAt(0);
                MoveToPosition(nextPosition);
                return true;
            }

            // Check for pending enemy now that we've reached our destination
            if (_pendingEnemyPosition != null && CurrentMode == ExplorationMode.FrontierExploration)
            {
                ILog.Print($"Reached destination, now pursuing pending enemy at {_pendingEnemyPosition}.");
                SetMode(ExplorationMode.PathToEnemy);
                _lastKnownEnemyPosition = _pendingEnemyPosition;
                _pendingEnemyPosition = null;
            }

            // Find next target using current strategy
            var context = new ExplorationContext
            {
                MapData = _mapData,
                CurrentPosition = CurrentPosition,
                FrontierBehavior = _frontierBehavior,
                VisibleEnemyPosition = VisibleEnemyPosition,
                LastKnownEnemyPosition = _lastKnownEnemyPosition
            };
            var target = _currentStrategy.GetNextTarget(context);

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
            // Only consider enemies within our current fog of war visibility
            // (respects both vision range AND line-of-sight)
            if (_frontierBehavior.CurrentlyVisibleTiles.Contains(enemyPos))
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
            // Enemy is visible - always track position for visibility purposes
            VisibleEnemyPosition = closestVisibleEnemy;
            _lastKnownEnemyPosition = closestVisibleEnemy;

            if (CurrentMode == ExplorationMode.FrontierExploration && _pathToTarget.Count > 0)
            {
                // We're exploring with an active path - defer enemy pursuit
                if (_pendingEnemyPosition != closestVisibleEnemy)
                {
                    ILog.Print($"Enemy spotted at {closestVisibleEnemy}! Deferring pursuit until current destination reached.");
                    _pendingEnemyPosition = closestVisibleEnemy;
                    EnemySpotted?.Invoke(closestVisibleEnemy.Value);
                }
                // Don't switch mode or clear path - continue to current destination
            }
            else
            {
                // No active path or already pursuing - switch to pursuit immediately
                if (CurrentMode != ExplorationMode.PathToEnemy)
                {
                    ILog.Print($"Enemy spotted at {closestVisibleEnemy}! Switching to pursuit mode.");
                    EnemySpotted?.Invoke(closestVisibleEnemy.Value);
                }
                _pendingEnemyPosition = null; // Clear pending since we're pursuing now

                // Only recalculate path if enemy moved or we don't have a path
                var shouldRecalculatePath = _pathToTarget.Count == 0 ||
                    _currentTargetTile != closestVisibleEnemy.Value;

                SetMode(ExplorationMode.PathToEnemy);

                if (shouldRecalculatePath)
                {
                    _pathToTarget.Clear();
                    _currentTargetTile = null;
                    PathUpdated?.Invoke();
                }
            }
        }
        else if (_lastKnownEnemyPosition != null)
        {
            // Enemy not visible but we have a last known position - continue toward it
            // Only clear and return to exploration if we've reached the last known position
            if (CurrentPosition == _lastKnownEnemyPosition.Value)
            {
                // We've reached the last known position but no enemy here - it must have moved or we were wrong
                ILog.Print($"Reached last known enemy position {_lastKnownEnemyPosition} but no enemy found. Returning to exploration.");
                _lastKnownEnemyPosition = null;
                VisibleEnemyPosition = null;
                SetMode(ExplorationMode.FrontierExploration);
                _pathToTarget.Clear();
                _currentTargetTile = null;
                PathUpdated?.Invoke();
            }
            else
            {
                // Still moving toward last known position - stay in pursuit mode
                ILog.Print($"Enemy not visible, continuing toward last known position {_lastKnownEnemyPosition}.");
                VisibleEnemyPosition = null; // Clear visible but keep pursuing
                // Don't clear path - let it continue
            }
        }
        else
        {
            // No enemies visible and no last known position - stay in exploration mode
            if (CurrentMode == ExplorationMode.PathToEnemy)
            {
                ILog.Print("Enemy no longer visible and no last known position. Returning to exploration.");
                _pathToTarget.Clear();
                _currentTargetTile = null;
                PathUpdated?.Invoke();
            }
            SetMode(ExplorationMode.FrontierExploration);
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

        PlayerMoved?.Invoke(CurrentPosition);
        VisitedTilesUpdated?.Invoke(_frontierBehavior.VisitedTiles);
        VisibilityUpdated?.Invoke(_frontierBehavior.SeenTiles, _frontierBehavior.CurrentlyVisibleTiles);
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
