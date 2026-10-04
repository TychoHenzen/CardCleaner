using System;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Exploration;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.ExplorationAISupport;

internal sealed class ExplorationStepRunner
{
    private readonly IMapData _mapData;
    private readonly Pathfinder _pathfinder;
    private readonly FrontierExplorationBehavior _frontierBehavior;
    private readonly ExplorationState _state;
    private readonly VisibleEnemyTracker _visibleEnemyTracker;
    private readonly ExplorationStepCallbacks _callbacks;

    public ExplorationStepRunner(
        IMapData mapData,
        Pathfinder pathfinder,
        FrontierExplorationBehavior frontierBehavior,
        ExplorationState state,
        VisibleEnemyTracker visibleEnemyTracker,
        ExplorationStepCallbacks callbacks)
    {
        _mapData = mapData;
        _pathfinder = pathfinder;
        _frontierBehavior = frontierBehavior;
        _state = state;
        _visibleEnemyTracker = visibleEnemyTracker;
        _callbacks = callbacks;
    }

    public bool Step()
    {
        if (TryHandleEnemyEncounter())
            return false;

        _visibleEnemyTracker.CheckForVisibleEnemies();

        if (HasFinishedExploration())
            return false;

        if (TryFollowExistingPath())
            return true;

        ActivatePendingEnemy();
        return FindAndMoveToNextTarget();
    }

    private bool TryHandleEnemyEncounter()
    {
        if (!_mapData.EnemySpawnCells.Contains(_state.CurrentCellId))
            return false;

        _state.HasFoundEnemy = true;
        _state.VisibleEnemyCellId = _state.CurrentCellId;
        ILog.Print($"Enemy encountered at cell {_state.CurrentCellId}!");
        _callbacks.EnemyEncountered(_mapData.GetCellCenter(_state.CurrentCellId));
        return true;
    }

    private bool HasFinishedExploration()
    {
        return _state.HasFoundEnemy ||
            (_frontierBehavior.IsFullyExplored() &&
             _state.CurrentMode != ExplorationMode.PathToEnemy &&
             _state.PendingEnemyCell == null);
    }

    private bool TryFollowExistingPath()
    {
        if (_state.PathToTarget.Count == 0)
            return false;

        var nextCell = _state.PathToTarget[0];
        _state.PathToTarget.RemoveAt(0);
        _callbacks.MoveToCell(nextCell);
        return true;
    }

    private void ActivatePendingEnemy()
    {
        if (_state.PendingEnemyCell == null ||
            _state.CurrentMode != ExplorationMode.FrontierExploration)
            return;

        ILog.Print(
            $"Reached destination, now pursuing pending enemy at cell {_state.PendingEnemyCell}.");
        _callbacks.SetMode(ExplorationMode.PathToEnemy);
        _state.LastKnownEnemyCell = _state.PendingEnemyCell;
        _state.PendingEnemyCell = null;
    }

    private bool FindAndMoveToNextTarget()
    {
        var context = new ExplorationContext
        {
            MapData = _mapData,
            CurrentCellId = _state.CurrentCellId,
            FrontierBehavior = _frontierBehavior,
            VisibleEnemyCellId = _state.VisibleEnemyCellId,
            LastKnownEnemyCellId = _state.LastKnownEnemyCell
        };
        var target = _callbacks.GetCurrentStrategy().GetNextTarget(context);

        if (target == null)
            return FinishWithoutTarget();

        return MoveToTarget(target.Value);
    }

    private bool FinishWithoutTarget()
    {
        _state.CurrentTargetCell = null;
        _callbacks.PathUpdated();
        ILog.Print("No more targets to explore - exploration complete");
        return false;
    }

    private bool MoveToTarget(int targetCellId)
    {
        _state.PathToTarget.Clear();
        _state.CurrentTargetCell = targetCellId;
        var path = _pathfinder.FindPath(_state.CurrentCellId, targetCellId);

        if (path.Count <= 1)
            return HandleUnreachableTarget(targetCellId);

        _state.PathToTarget.AddRange(path.Skip(1));
        _callbacks.PathUpdated();
        return TryFollowExistingPath();
    }

    private bool HandleUnreachableTarget(int targetCellId)
    {
        _state.CurrentTargetCell = null;
        _callbacks.PathUpdated();
        ILog.Print(
            $"No path to target cell {targetCellId} found from cell " +
            $"{_state.CurrentCellId} - exploration stuck");
        return false;
    }
}
