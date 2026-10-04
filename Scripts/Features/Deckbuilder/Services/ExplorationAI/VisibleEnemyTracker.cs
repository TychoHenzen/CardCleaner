using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.ExplorationAISupport;

internal sealed class VisibleEnemyTracker
{
    private readonly IMapData _mapData;
    private readonly FrontierExplorationBehavior _frontierBehavior;
    private readonly ExplorationState _state;
    private readonly Action<ExplorationMode> _setMode;
    private readonly Action<Vector2> _enemySpotted;
    private readonly Action _pathUpdated;

    public VisibleEnemyTracker(
        IMapData mapData,
        FrontierExplorationBehavior frontierBehavior,
        ExplorationState state,
        Action<ExplorationMode> setMode,
        Action<Vector2> enemySpotted,
        Action pathUpdated)
    {
        _mapData = mapData;
        _frontierBehavior = frontierBehavior;
        _state = state;
        _setMode = setMode;
        _enemySpotted = enemySpotted;
        _pathUpdated = pathUpdated;
    }

    public void CheckForVisibleEnemies()
    {
        var closestVisibleEnemy = FindClosestVisibleEnemy();
        if (closestVisibleEnemy.HasValue)
        {
            HandleVisibleEnemy(closestVisibleEnemy.Value);
            return;
        }

        HandleNoVisibleEnemy();
    }

    private int? FindClosestVisibleEnemy()
    {
        int? closestVisibleEnemy = null;
        var closestDistanceSquared = float.MaxValue;
        var currentPosition = _mapData.GetCellCenter(_state.CurrentCellId);

        foreach (var enemyCellId in _mapData.EnemySpawnCells)
        {
            if (!_frontierBehavior.CurrentlyVisibleCells.Contains(enemyCellId))
                continue;

            var enemyPosition = _mapData.GetCellCenter(enemyCellId);
            var distanceSquared = currentPosition.DistanceSquaredTo(enemyPosition);
            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestVisibleEnemy = enemyCellId;
            }
        }

        return closestVisibleEnemy;
    }

    private void HandleVisibleEnemy(int enemyCellId)
    {
        _state.VisibleEnemyCellId = enemyCellId;
        _state.LastKnownEnemyCell = enemyCellId;

        if (_state.CurrentMode == ExplorationMode.FrontierExploration &&
            _state.PathToTarget.Count > 0)
        {
            DeferEnemyPursuit(enemyCellId);
            return;
        }

        SwitchToEnemyPursuit(enemyCellId);
    }

    private void DeferEnemyPursuit(int enemyCellId)
    {
        if (_state.PendingEnemyCell == enemyCellId)
            return;

        ILog.Print(
            $"Enemy spotted at cell {enemyCellId}! " +
            "Deferring pursuit until current destination reached.");
        _state.PendingEnemyCell = enemyCellId;
        _enemySpotted(_mapData.GetCellCenter(enemyCellId));
    }

    private void SwitchToEnemyPursuit(int enemyCellId)
    {
        if (_state.CurrentMode != ExplorationMode.PathToEnemy)
        {
            ILog.Print($"Enemy spotted at cell {enemyCellId}! Switching to pursuit mode.");
            _enemySpotted(_mapData.GetCellCenter(enemyCellId));
        }

        _state.PendingEnemyCell = null;
        var shouldRecalculatePath = _state.PathToTarget.Count == 0 ||
            _state.CurrentTargetCell != enemyCellId;

        _setMode(ExplorationMode.PathToEnemy);

        if (shouldRecalculatePath)
            ClearPath();
    }

    private void HandleNoVisibleEnemy()
    {
        if (_state.LastKnownEnemyCell.HasValue)
        {
            HandleLastKnownEnemy();
            return;
        }

        HandleNoLastKnownEnemy();
    }

    private void HandleLastKnownEnemy()
    {
        if (_state.LastKnownEnemyCell is { } lastKnownEnemyCell &&
            _state.CurrentCellId == lastKnownEnemyCell)
        {
            ILog.Print(
                $"Reached last known enemy cell {_state.LastKnownEnemyCell} but no enemy found. " +
                "Returning to exploration.");
            _state.LastKnownEnemyCell = null;
            _state.VisibleEnemyCellId = null;
            _setMode(ExplorationMode.FrontierExploration);
            ClearPath();
            return;
        }

        ILog.Print(
            $"Enemy not visible, continuing toward last known cell {_state.LastKnownEnemyCell}.");
        _state.VisibleEnemyCellId = null;
    }

    private void HandleNoLastKnownEnemy()
    {
        if (_state.CurrentMode == ExplorationMode.PathToEnemy)
        {
            ILog.Print("Enemy no longer visible and no last known position. Returning to exploration.");
            ClearPath();
        }

        _setMode(ExplorationMode.FrontierExploration);
        _state.VisibleEnemyCellId = null;
    }

    private void ClearPath()
    {
        _state.PathToTarget.Clear();
        _state.CurrentTargetCell = null;
        _pathUpdated();
    }
}
