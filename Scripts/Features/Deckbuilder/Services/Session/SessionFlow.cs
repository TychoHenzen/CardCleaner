using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using Timer = Godot.Timer;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Drives the session after its map exists: the state machine, the game timer,
/// exploration steps paced by the view, combat turns, loot, and cleanup.
/// </summary>
internal sealed class SessionFlow
{
    private const float ExplorationStepDelay = 0.1f;
    private const float CombatTurnDelay = 1.0f;

    private readonly Node _owner;
    private readonly ISessionWorld _world;
    private readonly IExplorationSession _exploration;
    private Timer _gameTimer = null!;
    private SessionState _state = SessionState.WaitingForCards;
    private ICombatSystem? _combatSystem;
    // Moves the view has reported shown. Exploration waits while the session is ahead of the view.
    private int _shownMoves;
    private bool _waitingForView;

    internal event Action<SessionState>? StateChanged;
    internal event Action<List<CardSignature>>? LootGenerated;

    internal SessionFlow(
        Node owner,
        ISessionWorld world,
        IExplorationSession exploration)
    {
        _owner = owner;
        _world = world;
        _exploration = exploration;
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
        if (IsAheadOfView())
        {
            _waitingForView = true;
            return;
        }

        _gameTimer.Start();
    }

    /// <summary>
    /// Records that the view has shown one more move. Once the view has shown every move, exploration takes its
    /// next step after the usual delay; the step is never taken inside this call.
    /// </summary>
    internal void ReportStepShown()
    {
        if (IsAheadOfView())
            _shownMoves++;

        if (!_waitingForView || IsAheadOfView())
            return;

        _waitingForView = false;
        if (State == SessionState.Exploring)
            _gameTimer.Start();
    }

    private bool IsAheadOfView() => _shownMoves < _exploration.MoveCount;

    internal void StartCombat(List<CardSignature> abilityCards, CardSignature enemySeed)
    {
        ILog.Print($"Starting combat with {abilityCards.Count} ability cards...");

        _combatSystem = SessionFactories.CreateCombat(abilityCards, enemySeed, _world.Rng);
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
        _shownMoves = 0;
        _waitingForView = false;
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
            default:
                WaitForViewIfAhead();
                break;
        }
    }

    /// <summary>
    /// Holds exploration after a step that moved the player until the view reports the move shown.
    /// </summary>
    private void WaitForViewIfAhead()
    {
        if (!IsAheadOfView())
            return;

        _waitingForView = true;
        _gameTimer.Stop();
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

        _combatSystem = null;
        _exploration.Clear();
        _shownMoves = 0;
        _waitingForView = false;
    }
}
