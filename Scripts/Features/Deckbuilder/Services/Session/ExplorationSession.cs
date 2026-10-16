using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Runs autonomous exploration for a session and raises its events with cell ids and world positions, the same on
/// both map kinds; each view converts them to its own coordinates.
/// </summary>
internal sealed class ExplorationSession : IExplorationSession
{
    private readonly ISessionWorld _world;
    private IExplorationAI? _explorationAI;
    private Vector2? _playerWorldPosition;
    private int? _currentEnemyCellId;
    /// <summary>
    /// Moves the player has made since the session's map was cleared; the session paces exploration against it.
    /// </summary>
    public int MoveCount { get; private set; }

    /// <summary>
    /// The line-of-sight checker the session's view handed over, or null for the registered or the simple checker.
    /// </summary>
    public IVisibilityChecker? VisibilityChecker { get; set; }

    public event Action<Vector2>? PlayerMovedWorld;
    public event Action<int>? EnemyDefeatedCell;
    public event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    public event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;
    public event Action<IReadOnlyList<int>, int?>? PathCellsUpdated;

    internal ExplorationSession(ISessionWorld world)
    {
        _world = world;
    }

    /// <summary>
    /// Starts exploring the current map. Returns false when there is no map to explore.
    /// </summary>
    public bool Start()
    {
        ILog.Print($"Starting exploration... ({_world.EnemyCount} enemies on map)");

        // Check if we have valid map data (from either regular or custom generator)
        if (_world.MapData == null)
        {
            ILog.Error("Cannot start exploration - no map data available");
            return false;
        }

        _explorationAI = ExplorationAIs.Create(_world.MapData, ResolveStartCellId(), VisibilityChecker);

        _explorationAI.EnemyEncountered += OnEnemyEncounteredWorld;
        _explorationAI.PlayerMoved += OnPlayerMovedWorld;
        _explorationAI.VisitedCellsUpdated += cellIds => VisitedCellsUpdated?.Invoke(cellIds);
        _explorationAI.VisibilityUpdated += (seen, visible) => VisibilityCellsUpdated?.Invoke(seen, visible);
        _explorationAI.PathUpdated += OnPathUpdated;
        return true;
    }

    public ExplorationStepOutcome Step()
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
    public void ResolveDefeatedEnemy()
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

    public void Clear()
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
