using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Timer = Godot.Timer;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Drives the session after its map exists: the state machine, the game timer,
/// exploration steps, combat turns, loot, and cleanup.
/// </summary>
internal sealed class SessionFlow
{
    private const float ExplorationStepDelay = 0.1f;
    private const float CombatTurnDelay = 1.0f;

    private readonly Node _owner;
    private readonly SessionWorld _world;
    private readonly GridCellEventAdapter _eventAdapter;
    private readonly ExplorationSession _exploration;
    private Timer _gameTimer = null!;
    private SessionState _state = SessionState.WaitingForCards;
    private SimpleCombatSystem? _combatSystem;

    internal event Action<SessionState>? StateChanged;
    internal event Action<List<CardSignature>>? LootGenerated;

    internal SessionFlow(
        Node owner,
        SessionWorld world,
        GridCellEventAdapter eventAdapter,
        ExplorationSession exploration)
    {
        _owner = owner;
        _world = world;
        _eventAdapter = eventAdapter;
        _exploration = exploration;
        _exploration.EnemyEncountered += () => EnterState(SessionState.InCombat);
    }

    internal SessionState State
    {
        get => _state;
        set
        {
            if (_state == value)
                return;
            _state = value;
            StateChanged?.Invoke(value);
            ILog.Print($"Session state changed to: {value}");
        }
    }

    /// <summary>
    /// Drives the session from the given timer, which the owner node has already added to the tree.
    /// </summary>
    internal void AttachTimer(Timer gameTimer)
    {
        _gameTimer = gameTimer;
        _gameTimer.Timeout += OnTimerTimeout;
    }

    internal void StartExploration()
    {
        if (!_exploration.Start())
            return;

        _gameTimer.WaitTime = ExplorationStepDelay;
        _gameTimer.Start();
    }

    internal void StartCombat(List<CardSignature> abilityCards, CardSignature enemySeed)
    {
        ILog.Print($"Starting combat with {abilityCards.Count} ability cards...");

        _combatSystem = new SimpleCombatSystem(abilityCards, enemySeed, _world.Rng);
        _combatSystem.CombatEnded += OnCombatEnded;

        // Start combat timer
        _gameTimer.WaitTime = CombatTurnDelay;
        _gameTimer.Start();
    }

    internal void GenerateLoot(CardSignature baseSeed)
    {
        ILog.Print("Generating loot from defeated enemy...");

        var lootSignatures = LootGenerator.Generate(baseSeed, _world.Rng);

        CleanupCurrentSession();

        State = SessionState.SessionComplete;
        LootGenerated?.Invoke(lootSignatures);

        ILog.Print($"Session complete! Generated {lootSignatures.Count} loot cards");
        _owner.CallDeferred(GameSessionService.MethodName.ResetForNextSession);
    }

    /// <summary>
    /// Stops the timer and drops the map, exploration, and combat of an abandoned session.
    /// </summary>
    internal void Reset()
    {
        _gameTimer.Stop();
        _world.ClearMap();
        _exploration.Clear();
        _combatSystem = null;
    }

    private void OnTimerTimeout()
    {
        switch (State)
        {
            case SessionState.Exploring:
                ProcessExplorationStep();
                break;
            case SessionState.InCombat:
                ProcessCombatTurn();
                break;
        }
    }

    private void ProcessExplorationStep()
    {
        switch (_exploration.Step())
        {
            case ExplorationStepOutcome.EnemyFound:
                EnterState(SessionState.InCombat);
                break;
            case ExplorationStepOutcome.Completed:
                EnterState(SessionState.GeneratingLoot);
                break;
        }
    }

    /// <summary>
    /// Stops the game timer, switches state, and advances the session on the next idle frame.
    /// </summary>
    private void EnterState(SessionState state)
    {
        _gameTimer.Stop();
        State = state;
        _owner.CallDeferred(GameSessionService.MethodName.AdvanceSession);
    }

    private void ProcessCombatTurn()
    {
        if (_combatSystem == null) return;

        var shouldContinue = _combatSystem.ProcessTurn();

        if (!shouldContinue) _gameTimer.Stop();
    }

    private void OnCombatEnded()
    {
        if (_combatSystem?.PlayerWon != true)
        {
            ILog.Print("Combat lost! Session ending...");
            CleanupCurrentSession();
            State = SessionState.SessionComplete;
            _owner.CallDeferred(GameSessionService.MethodName.ResetForNextSession);
            return;
        }

        _exploration.ResolveDefeatedEnemy();

        State = NextStateAfterVictory();
        _owner.CallDeferred(GameSessionService.MethodName.AdvanceSession);
    }

    private SessionState NextStateAfterVictory()
    {
        if (_world.EnemyCount > 0)
        {
            ILog.Print("Resuming exploration to find remaining enemies...");
            return SessionState.Exploring;
        }

        ILog.Print("All enemies destroyed! Generating loot...");
        return SessionState.GeneratingLoot;
    }

    private void CleanupCurrentSession()
    {
        _gameTimer?.Stop();

        _world.ClearMap();
        _eventAdapter.SetGridMapData(null);

        _combatSystem = null;
        _exploration.Clear();
    }
}
