using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Runs autonomous exploration for a session and translates exploration events
/// into both cell-based and grid-based events.
/// </summary>
internal sealed class ExplorationSession
{
    private readonly SessionWorld _world;
    private readonly GridCellEventAdapter _eventAdapter;
    private ExplorationAI? _explorationAI;
    private Vector2? _playerWorldPosition;
    private Vector2I? _playerPosition;
    private int? _currentEnemyCellId;
    private Vector2I? _currentEnemyPosition;

    internal event Action<Vector2I>? PlayerMoved;
    internal event Action<Vector2>? PlayerMovedWorld;
    internal event Action<Vector2I>? EnemyDefeated;
    internal event Action<int>? EnemyDefeatedCell;
    internal event Action<IReadOnlySet<Vector2I>>? VisitedTilesUpdated;
    internal event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    internal event Action<IReadOnlySet<Vector2I>, IReadOnlySet<Vector2I>>? VisibilityUpdated;
    internal event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;
    internal event Action<IReadOnlyList<Vector2I>, Vector2I?>? PathUpdated;
    internal event Action<IReadOnlyList<int>, int?>? PathCellsUpdated;
    internal event Action? EnemyEncountered;

    internal ExplorationSession(SessionWorld world, GridCellEventAdapter eventAdapter)
    {
        _world = world;
        _eventAdapter = eventAdapter;
    }

    /// <summary>
    /// Starts exploring the current map. Returns false when there is no map to explore.
    /// </summary>
    internal bool Start()
    {
        ILog.Print($"Starting exploration... ({_world.EnemyCount} enemies on map)");

        // Check if we have valid map data (from either regular or custom generator)
        if (_world.MapData == null)
        {
            ILog.Error("Cannot start exploration - no map data available");
            return false;
        }

        _explorationAI = new ExplorationAI(_world.MapData, ResolveStartCellId());

        // Subscribe with adapters to emit both Vector2I and cell-based events
        _explorationAI.EnemyEncountered += OnEnemyEncounteredWorld;
        _explorationAI.PlayerMoved += OnPlayerMovedWorld;
        _explorationAI.VisitedCellsUpdated += OnVisitedCellsUpdated;
        _explorationAI.VisibilityUpdated += OnVisibilityUpdated;
        _explorationAI.PathUpdated += OnPathUpdated;
        return true;
    }

    internal ExplorationStepOutcome Step()
    {
        if (_explorationAI == null || _explorationAI.StepExploration())
            return ExplorationStepOutcome.Continuing;

        if (_explorationAI.HasFoundEnemy)
            return ExplorationStepOutcome.EnemyFound;

        ILog.Print("Exploration complete - no enemies found, ending session");
        return ExplorationStepOutcome.Completed;
    }

    /// <summary>
    /// Moves the player onto the defeated enemy's cell and removes the enemy from the map.
    /// </summary>
    internal void ResolveDefeatedEnemy()
    {
        if (!_currentEnemyCellId.HasValue) return;

        var defeatedCellId = _currentEnemyCellId.Value;

        // Update player position to enemy's position
        _playerWorldPosition = _world.MapData?.GetCellCenter(defeatedCellId);
        _playerPosition = _eventAdapter.GetGridPosition(defeatedCellId);

        // Remove enemy using IGeneratedMap interface
        _world.GeneratedMap?.RemoveEnemyAt(defeatedCellId);
        var remainingEnemies = _world.GeneratedMap?.EnemyCount ?? 0;
        ILog.Print($"Enemy at cell {defeatedCellId} destroyed! ({remainingEnemies} enemies remaining)");

        // Emit events in both coordinate systems
        _eventAdapter.EmitEnemyDefeated(defeatedCellId, EnemyDefeatedCell, EnemyDefeated);

        // Update legacy map data if present
        if (_currentEnemyPosition.HasValue)
        {
            _world.LegacyMap?.EnemyPositions.Remove(_currentEnemyPosition.Value);
            _currentEnemyPosition = null;
        }

        _currentEnemyCellId = null;
    }

    internal void Clear()
    {
        _explorationAI = null;
        _playerPosition = null;
        _playerWorldPosition = null;
        _currentEnemyPosition = null;
        _currentEnemyCellId = null;
    }

    private int? ResolveStartCellId()
    {
        if (_playerWorldPosition.HasValue)
        {
            // Resume from world position (custom generator case)
            return _world.MapData!.GetCellAtPosition(_playerWorldPosition.Value);
        }

        if (_playerPosition.HasValue && _world.GridMapData != null)
        {
            // Resume from grid position (regular generator case)
            return _world.GridMapData.PositionToCellId(_playerPosition.Value);
        }

        return null;
    }

    private void OnPlayerMovedWorld(Vector2 worldPos)
    {
        _playerWorldPosition = worldPos;
        _playerPosition = _eventAdapter.WorldToGrid(worldPos);

        _eventAdapter.EmitPlayerMoved(worldPos, PlayerMovedWorld, PlayerMoved);
    }

    private void OnEnemyEncounteredWorld(Vector2 worldPos)
    {
        var cellId = _world.MapData?.GetCellAtPosition(worldPos);
        if (cellId.HasValue)
            _currentEnemyCellId = cellId.Value;

        if (!_eventAdapter.HasGridData)
        {
            ILog.Print($"Enemy encountered at world position {worldPos}! Preparing for combat...");
            EnemyEncountered?.Invoke();
            return;
        }

        var gridPos = _eventAdapter.WorldToGrid(worldPos);
        if (!gridPos.HasValue)
            return;

        ILog.Print($"Enemy encountered at {gridPos.Value}! Preparing for combat...");
        _currentEnemyPosition = gridPos.Value;
        EnemyEncountered?.Invoke();
    }

    private void OnVisitedCellsUpdated(IReadOnlySet<int> cellIds)
    {
        _eventAdapter.EmitVisitedCells(cellIds, VisitedCellsUpdated, VisitedTilesUpdated);
    }

    private void OnVisibilityUpdated(IReadOnlySet<int> seenCells, IReadOnlySet<int> visibleCells)
    {
        _eventAdapter.EmitVisibility(seenCells, visibleCells, VisibilityCellsUpdated, VisibilityUpdated);
    }

    private void OnPathUpdated()
    {
        if (_explorationAI == null) return;

        _eventAdapter.EmitPath(
            _explorationAI.CurrentPath,
            _explorationAI.CurrentTargetCell,
            PathCellsUpdated,
            PathUpdated);
    }
}
