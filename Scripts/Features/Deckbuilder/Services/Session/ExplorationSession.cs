using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Runs autonomous exploration for a session and raises its events with cell ids and world positions, the same on
/// both map kinds; each view converts them to its own coordinates.
/// </summary>
internal sealed class ExplorationSession
{
    private readonly SessionWorld _world;
    private ExplorationAI? _explorationAI;
    private Vector2? _playerWorldPosition;
    private int? _currentEnemyCellId;
    /// <summary>
    /// Moves the player has made since the session's map was cleared; the session paces exploration against it.
    /// </summary>
    internal int MoveCount { get; private set; }

    /// <summary>
    /// The line-of-sight checker the session's view handed over, or null for the registered or the simple checker.
    /// </summary>
    internal IVisibilityChecker? VisibilityChecker { get; set; }

    internal event Action<Vector2>? PlayerMovedWorld;
    internal event Action<int>? EnemyDefeatedCell;
    internal event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    internal event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;
    internal event Action<IReadOnlyList<int>, int?>? PathCellsUpdated;

    internal ExplorationSession(SessionWorld world)
    {
        _world = world;
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

        _explorationAI = new ExplorationAI(_world.MapData, ResolveStartCellId(), VisibilityChecker);

        _explorationAI.EnemyEncountered += OnEnemyEncounteredWorld;
        _explorationAI.PlayerMoved += OnPlayerMovedWorld;
        _explorationAI.VisitedCellsUpdated += cellIds => VisitedCellsUpdated?.Invoke(cellIds);
        _explorationAI.VisibilityUpdated += (seen, visible) => VisibilityCellsUpdated?.Invoke(seen, visible);
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

        // Remove enemy using IGeneratedMap interface
        _world.GeneratedMap?.RemoveEnemyAt(defeatedCellId);
        var remainingEnemies = _world.GeneratedMap?.EnemyCount ?? 0;
        ILog.Print($"Enemy at cell {defeatedCellId} destroyed! ({remainingEnemies} enemies remaining)");

        EnemyDefeatedCell?.Invoke(defeatedCellId);

        _currentEnemyCellId = null;
    }

    internal void Clear()
    {
        _explorationAI = null;
        _playerWorldPosition = null;
        _currentEnemyCellId = null;
        MoveCount = 0;
    }

    /// <summary>
    /// The cell to resume from after a fight, or null to start from the map's player start.
    /// </summary>
    private int? ResolveStartCellId() =>
        _playerWorldPosition.HasValue ? _world.MapData!.GetCellAtPosition(_playerWorldPosition.Value) : null;

    private void OnPlayerMovedWorld(Vector2 worldPos)
    {
        MoveCount++;
        _playerWorldPosition = worldPos;
        PlayerMovedWorld?.Invoke(worldPos);
    }

    /// <summary>
    /// Records which enemy the player reached. The step that raised this returns
    /// <see cref="ExplorationStepOutcome.EnemyFound"/>, and that outcome alone starts the combat.
    /// </summary>
    private void OnEnemyEncounteredWorld(Vector2 worldPos)
    {
        var cellId = _world.MapData?.GetCellAtPosition(worldPos);
        if (cellId.HasValue)
            _currentEnemyCellId = cellId.Value;

        ILog.Print($"Enemy encountered at cell {cellId} (world position {worldPos})! Preparing for combat...");
    }

    private void OnPathUpdated()
    {
        if (_explorationAI == null) return;

        PathCellsUpdated?.Invoke(_explorationAI.CurrentPath, _explorationAI.CurrentTargetCell);
    }
}
