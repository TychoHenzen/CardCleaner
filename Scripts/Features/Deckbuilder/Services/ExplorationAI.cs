using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;
using CardCleaner.Scripts.Features.Deckbuilder.Services.ExplorationAISupport;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// AI that explores a map using frontier-based exploration,
/// switching to enemy targeting when enemies become visible.
/// Now works with any IMapData implementation (regular or irregular grids).
/// </summary>
public class ExplorationAI
{
    private readonly IMapData _mapData;
    private readonly FrontierExplorationBehavior _frontierBehavior;
    private readonly ExplorationState _state;
    private readonly IExplorationStrategy _frontierStrategy;
    private readonly IExplorationStrategy _enemyPursuitStrategy;
    private readonly VisibleEnemyTracker _visibleEnemyTracker;
    private readonly ExplorationStepRunner _stepRunner;
    private IExplorationStrategy _currentStrategy;

    public int CurrentCellId => _state.CurrentCellId;
    public Vector2 CurrentPosition => _mapData.GetCellCenter(CurrentCellId);
    public bool HasFoundEnemy => _state.HasFoundEnemy;
    public int? VisibleEnemyCellId => _state.VisibleEnemyCellId;
    public Vector2? VisibleEnemyPosition => VisibleEnemyCellId.HasValue
        ? _mapData.GetCellCenter(VisibleEnemyCellId.Value)
        : null;
    public ExplorationMode CurrentMode => _state.CurrentMode;

    public bool HasFinishedExploration =>
        HasFoundEnemy ||
        (_frontierBehavior.IsFullyExplored() &&
         CurrentMode != ExplorationMode.PathToEnemy &&
         _state.PendingEnemyCell == null);

    public IReadOnlySet<int> SeenCells => _frontierBehavior.SeenCells;
    public IReadOnlySet<int> CurrentlyVisibleCells => _frontierBehavior.CurrentlyVisibleCells;

    /// <summary>
    /// The current path being followed as cell IDs (for debug visualization).
    /// </summary>
    public IReadOnlyList<int> CurrentPath => _state.PathToTarget;

    /// <summary>
    /// The current path as world positions.
    /// </summary>
    public IEnumerable<Vector2> CurrentPathPositions =>
        _state.PathToTarget.Select(cellId => _mapData.GetCellCenter(cellId));

    /// <summary>
    /// The current target cell the agent is trying to reach.
    /// </summary>
    public int? CurrentTargetCell => _state.CurrentTargetCell;

    /// <summary>
    /// The current target as world position.
    /// </summary>
    public Vector2? CurrentTargetPosition => _state.CurrentTargetCell.HasValue
        ? _mapData.GetCellCenter(_state.CurrentTargetCell.Value)
        : null;

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
        _state.HasFoundEnemy = false;
        _state.VisibleEnemyCellId = null;
        _state.LastKnownEnemyCell = null;
        _state.PendingEnemyCell = null;
        SetMode(ExplorationMode.FrontierExploration);
        _state.PathToTarget.Clear();
        _state.CurrentTargetCell = null;
        PathUpdated?.Invoke();
        ILog.Print("Combat state reset - exploration can continue");
    }

    /// <summary>
    /// Create exploration AI with a map data provider.
    /// </summary>
    public ExplorationAI(
        IMapData mapData,
        int? startCell = null,
        IVisibilityChecker? visibilityChecker = null,
        int visionRange = 5,
        IFogOfWar? fogOfWar = null)
    {
        ArgumentNullException.ThrowIfNull(mapData);

        _mapData = mapData;
        var resolvedVisibilityChecker = visibilityChecker ?? (ServiceLocator.Has<IVisibilityChecker>()
            ? ServiceLocator.Get<IVisibilityChecker>()
            : new SimpleVisibilityChecker());
        _frontierBehavior = new FrontierExplorationBehavior(
            mapData,
            resolvedVisibilityChecker,
            visionRange,
            fogOfWar);
        _frontierStrategy = new FrontierExplorationStrategy();
        _enemyPursuitStrategy = new PathToEnemyStrategy();
        _currentStrategy = _frontierStrategy;
        _state = new ExplorationState
        {
            CurrentCellId = startCell ?? mapData.PlayerStartCell ?? 0
        };
        _state.VisitedCells.Add(CurrentCellId);

        _visibleEnemyTracker = new VisibleEnemyTracker(
            mapData,
            _frontierBehavior,
            _state,
            SetMode,
            position => EnemySpotted?.Invoke(position),
            () => PathUpdated?.Invoke());
        _stepRunner = new ExplorationStepRunner(
            mapData,
            new Pathfinder(mapData),
            _frontierBehavior,
            _state,
            _visibleEnemyTracker,
            new ExplorationStepCallbacks
            {
                GetCurrentStrategy = () => _currentStrategy,
                SetMode = SetMode,
                MoveToCell = MoveToCell,
                EnemyEncountered = position => EnemyEncountered?.Invoke(position),
                PathUpdated = () => PathUpdated?.Invoke()
            });

        UpdateInitialVision();
        ILog.Print(
            $"Exploration AI initialized at cell {CurrentCellId} (position: {CurrentPosition})");
    }

    /// <summary>
    /// Backwards-compatible constructor that wraps SimpleMapData in RegularGridMapData.
    /// </summary>
    public ExplorationAI(
        SimpleMapData simpleMapData,
        Vector2I? startPosition = null,
        IVisibilityChecker? visibilityChecker = null,
        int visionRange = 5)
        : this(
            new RegularGridMapData(simpleMapData),
            startPosition.HasValue
                ? startPosition.Value.Y * simpleMapData.Size.X + startPosition.Value.X
                : null,
            visibilityChecker,
            visionRange)
    {
    }

    private void UpdateInitialVision()
    {
        try
        {
            _frontierBehavior.UpdateVision(CurrentCellId);
            VisitedCellsUpdated?.Invoke(_frontierBehavior.VisitedCells);
            VisibilityUpdated?.Invoke(
                _frontierBehavior.SeenCells,
                _frontierBehavior.CurrentlyVisibleCells);
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception during initial vision update: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void SetMode(ExplorationMode mode)
    {
        _state.CurrentMode = mode;
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
            return _stepRunner.Step();
        }
        catch (Exception ex)
        {
            ILog.Error($"Exception in StepExploration: {ex.Message}\n{ex.StackTrace}");
            return false;
        }
    }

    private void MoveToCell(int newCellId)
    {
        if (!_mapData.IsPassable(newCellId))
        {
            ILog.Error($"Attempted to move to blocked cell {newCellId}");
            return;
        }

        _state.CurrentCellId = newCellId;
        _state.VisitedCells.Add(CurrentCellId);
        _frontierBehavior.UpdateVision(CurrentCellId);

        PlayerMoved?.Invoke(CurrentPosition);
        VisitedCellsUpdated?.Invoke(_frontierBehavior.VisitedCells);
        VisibilityUpdated?.Invoke(
            _frontierBehavior.SeenCells,
            _frontierBehavior.CurrentlyVisibleCells);
    }
}
