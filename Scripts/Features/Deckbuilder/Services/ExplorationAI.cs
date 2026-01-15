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
/// Now works with any IMapData implementation (regular or irregular grids).
/// </summary>
public class ExplorationAI
{
    private readonly IMapData _mapData;
    private readonly Pathfinder _pathfinder;
    private readonly HashSet<int> _visitedCells = new();
    private readonly List<int> _pathToTarget = new();
    private readonly FrontierExplorationBehavior _frontierBehavior;
    private readonly IVisibilityChecker _visibilityChecker;
    private readonly IExplorationStrategy _frontierStrategy;
    private readonly IExplorationStrategy _enemyPursuitStrategy;
    private IExplorationStrategy _currentStrategy;
    private int? _currentTargetCell;
    private int? _lastKnownEnemyCell;
    private int? _pendingEnemyCell;

    public int CurrentCellId { get; private set; }
    public Vector2 CurrentPosition => _mapData.GetCellCenter(CurrentCellId);
    public bool HasFoundEnemy { get; private set; }
    public int? VisibleEnemyCellId { get; private set; }
    public Vector2? VisibleEnemyPosition => VisibleEnemyCellId.HasValue
        ? _mapData.GetCellCenter(VisibleEnemyCellId.Value)
        : null;
    public ExplorationMode CurrentMode { get; private set; } = ExplorationMode.FrontierExploration;

    public bool HasFinishedExploration =>
        HasFoundEnemy ||
        (_frontierBehavior.IsFullyExplored() &&
         CurrentMode != ExplorationMode.PathToEnemy &&
         _pendingEnemyCell == null);
    public IReadOnlySet<int> SeenCells => _frontierBehavior.SeenCells;
    public IReadOnlySet<int> CurrentlyVisibleCells => _frontierBehavior.CurrentlyVisibleCells;

    /// <summary>
    /// The current path being followed as cell IDs (for debug visualization).
    /// </summary>
    public IReadOnlyList<int> CurrentPath => _pathToTarget;

    /// <summary>
    /// The current path as world positions.
    /// </summary>
    public IEnumerable<Vector2> CurrentPathPositions => _pathToTarget.Select(c => _mapData.GetCellCenter(c));

    /// <summary>
    /// The current target cell the agent is trying to reach.
    /// </summary>
    public int? CurrentTargetCell => _currentTargetCell;

    /// <summary>
    /// The current target as world position.
    /// </summary>
    public Vector2? CurrentTargetPosition => _currentTargetCell.HasValue
        ? _mapData.GetCellCenter(_currentTargetCell.Value)
        : null;

    // Events now use world positions (Vector2) instead of grid positions (Vector2I)
    public event Action<Vector2>? PlayerMoved;
    public event Action<Vector2>? EnemyEncountered;
    public event Action<Vector2>? EnemySpotted;
    public event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    public event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityUpdated;
    public event Action? PathUpdated;

    /// <summary>
    /// Reset combat-related state to allow exploration to continue after combat ends.
    /// Call this after successfully defeating an enemy and before resuming exploration.
    /// </summary>
    public void ResetCombatState()
    {
        HasFoundEnemy = false;
        VisibleEnemyCellId = null;
        _lastKnownEnemyCell = null;
        _pendingEnemyCell = null;
        SetMode(ExplorationMode.FrontierExploration);
        _pathToTarget.Clear();
        _currentTargetCell = null;
        PathUpdated?.Invoke();
        ILog.Print("Combat state reset - exploration can continue");
    }

    /// <summary>
    /// Create exploration AI with a map data provider.
    /// </summary>
    public ExplorationAI(IMapData mapData, int? startCell = null, IVisibilityChecker? visibilityChecker = null, int visionRange = 5, IFogOfWar? fogOfWar = null)
    {
        ArgumentNullException.ThrowIfNull(mapData);

        _mapData = mapData;
        _pathfinder = new Pathfinder(mapData);
        _visibilityChecker = visibilityChecker ?? (ServiceLocator.Has<IVisibilityChecker>()
            ? ServiceLocator.Get<IVisibilityChecker>()
            : new SimpleVisibilityChecker());

        _frontierBehavior = new FrontierExplorationBehavior(mapData, _visibilityChecker, visionRange, fogOfWar);

        // Initialize exploration strategies
        _frontierStrategy = new FrontierExplorationStrategy();
        _enemyPursuitStrategy = new PathToEnemyStrategy();
        _currentStrategy = _frontierStrategy;

        CurrentCellId = startCell ?? mapData.PlayerStartCell ?? 0;
        _visitedCells.Add(CurrentCellId);

        // Initial vision update
        try
        {
            _frontierBehavior.UpdateVision(CurrentCellId);
            VisitedCellsUpdated?.Invoke(_frontierBehavior.VisitedCells);
            VisibilityUpdated?.Invoke(_frontierBehavior.SeenCells, _frontierBehavior.CurrentlyVisibleCells);
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception during initial vision update: {ex.Message}\n{ex.StackTrace}");
        }

        ILog.Print($"Exploration AI initialized at cell {CurrentCellId} (position: {CurrentPosition})");
    }

    /// <summary>
    /// Backwards-compatible constructor that wraps SimpleMapData in RegularGridMapData.
    /// </summary>
    public ExplorationAI(SimpleMapData simpleMapData, Vector2I? startPosition = null, IVisibilityChecker? visibilityChecker = null, int visionRange = 5)
        : this(
            new RegularGridMapData(simpleMapData),
            startPosition.HasValue
                ? startPosition.Value.Y * simpleMapData.Size.X + startPosition.Value.X
                : null,
            visibilityChecker,
            visionRange)
    {
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
            if (_mapData.EnemySpawnCells.Contains(CurrentCellId))
            {
                HasFoundEnemy = true;
                VisibleEnemyCellId = CurrentCellId;
                ILog.Print($"Enemy encountered at cell {CurrentCellId}!");
                EnemyEncountered?.Invoke(CurrentPosition);
                return false;
            }

            // Check for visible enemies BEFORE checking if exploration is complete
            CheckForVisibleEnemies();

            if (HasFinishedExploration) return false;

            // If we have a path, follow it to completion (commit to destination)
            if (_pathToTarget.Count > 0)
            {
                var nextCell = _pathToTarget[0];
                _pathToTarget.RemoveAt(0);
                MoveToCell(nextCell);
                return true;
            }

            // Check for pending enemy now that we've reached our destination
            if (_pendingEnemyCell != null && CurrentMode == ExplorationMode.FrontierExploration)
            {
                ILog.Print($"Reached destination, now pursuing pending enemy at cell {_pendingEnemyCell}.");
                SetMode(ExplorationMode.PathToEnemy);
                _lastKnownEnemyCell = _pendingEnemyCell;
                _pendingEnemyCell = null;
            }

            // Find next target using current strategy
            var context = new ExplorationContext
            {
                MapData = _mapData,
                CurrentCellId = CurrentCellId,
                FrontierBehavior = _frontierBehavior,
                VisibleEnemyCellId = VisibleEnemyCellId,
                LastKnownEnemyCellId = _lastKnownEnemyCell
            };
            var target = _currentStrategy.GetNextTarget(context);

            if (target == null)
            {
                _currentTargetCell = null;
                PathUpdated?.Invoke();
                ILog.Print("No more targets to explore - exploration complete");
                return false;
            }

            // Calculate path to target using Pathfinder
            _pathToTarget.Clear();
            _currentTargetCell = target.Value;
            var path = _pathfinder.FindPath(CurrentCellId, target.Value);
            if (path.Count > 1)
            {
                // Take the first step now (skip current position)
                _pathToTarget.AddRange(path.Skip(1));
                PathUpdated?.Invoke();
                var nextCell = _pathToTarget[0];
                _pathToTarget.RemoveAt(0);
                MoveToCell(nextCell);
                return true;
            }

            _currentTargetCell = null;
            PathUpdated?.Invoke();
            ILog.Print($"No path to target cell {target.Value} found from cell {CurrentCellId} - exploration stuck");
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
        int? closestVisibleEnemy = null;
        var closestDistanceSquared = float.MaxValue;
        var currentPos = CurrentPosition;

        foreach (var enemyCellId in _mapData.EnemySpawnCells)
        {
            // Only consider enemies within our current fog of war visibility
            if (_frontierBehavior.CurrentlyVisibleCells.Contains(enemyCellId))
            {
                var enemyPos = _mapData.GetCellCenter(enemyCellId);
                var distanceSquared = currentPos.DistanceSquaredTo(enemyPos);
                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestVisibleEnemy = enemyCellId;
                }
            }
        }

        if (closestVisibleEnemy != null)
        {
            // Enemy is visible - always track position for visibility purposes
            VisibleEnemyCellId = closestVisibleEnemy;
            _lastKnownEnemyCell = closestVisibleEnemy;

            if (CurrentMode == ExplorationMode.FrontierExploration && _pathToTarget.Count > 0)
            {
                // We're exploring with an active path - defer enemy pursuit
                if (_pendingEnemyCell != closestVisibleEnemy)
                {
                    ILog.Print($"Enemy spotted at cell {closestVisibleEnemy}! Deferring pursuit until current destination reached.");
                    _pendingEnemyCell = closestVisibleEnemy;
                    EnemySpotted?.Invoke(_mapData.GetCellCenter(closestVisibleEnemy.Value));
                }
            }
            else
            {
                // No active path or already pursuing - switch to pursuit immediately
                if (CurrentMode != ExplorationMode.PathToEnemy)
                {
                    ILog.Print($"Enemy spotted at cell {closestVisibleEnemy}! Switching to pursuit mode.");
                    EnemySpotted?.Invoke(_mapData.GetCellCenter(closestVisibleEnemy.Value));
                }
                _pendingEnemyCell = null;

                // Only recalculate path if enemy moved or we don't have a path
                var shouldRecalculatePath = _pathToTarget.Count == 0 ||
                    _currentTargetCell != closestVisibleEnemy.Value;

                SetMode(ExplorationMode.PathToEnemy);

                if (shouldRecalculatePath)
                {
                    _pathToTarget.Clear();
                    _currentTargetCell = null;
                    PathUpdated?.Invoke();
                }
            }
        }
        else if (_lastKnownEnemyCell != null)
        {
            // Enemy not visible but we have a last known position - continue toward it
            if (CurrentCellId == _lastKnownEnemyCell.Value)
            {
                ILog.Print($"Reached last known enemy cell {_lastKnownEnemyCell} but no enemy found. Returning to exploration.");
                _lastKnownEnemyCell = null;
                VisibleEnemyCellId = null;
                SetMode(ExplorationMode.FrontierExploration);
                _pathToTarget.Clear();
                _currentTargetCell = null;
                PathUpdated?.Invoke();
            }
            else
            {
                ILog.Print($"Enemy not visible, continuing toward last known cell {_lastKnownEnemyCell}.");
                VisibleEnemyCellId = null;
            }
        }
        else
        {
            // No enemies visible and no last known position - stay in exploration mode
            if (CurrentMode == ExplorationMode.PathToEnemy)
            {
                ILog.Print("Enemy no longer visible and no last known position. Returning to exploration.");
                _pathToTarget.Clear();
                _currentTargetCell = null;
                PathUpdated?.Invoke();
            }
            SetMode(ExplorationMode.FrontierExploration);
            VisibleEnemyCellId = null;
        }
    }

    private void MoveToCell(int newCellId)
    {
        if (!_mapData.IsPassable(newCellId))
        {
            ILog.Error($"Attempted to move to blocked cell {newCellId}");
            return;
        }

        CurrentCellId = newCellId;
        _visitedCells.Add(CurrentCellId);

        // Update vision from new position
        _frontierBehavior.UpdateVision(CurrentCellId);

        PlayerMoved?.Invoke(CurrentPosition);
        VisitedCellsUpdated?.Invoke(_frontierBehavior.VisitedCells);
        VisibilityUpdated?.Invoke(_frontierBehavior.SeenCells, _frontierBehavior.CurrentlyVisibleCells);
    }
}
