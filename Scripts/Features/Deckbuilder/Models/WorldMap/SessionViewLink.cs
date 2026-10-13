using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Connects one map view to the game session. It starts the view's sessions, keeping a start until the service
/// connects, owns the subscriptions to the session's events and passes them on only while the view owns the session:
/// from the view's own start until the session falls back to waiting, or the view resets or disconnects.
/// </summary>
internal sealed class SessionViewLink
{
    private readonly ISessionView _view;
    private IGameSessionService? _session;
    private (CardSignature[] MapSeed, CardSignature[] Abilities, IMapGenerator Generator)? _pendingStart;

    internal SessionViewLink(ISessionView view)
    {
        _view = view;
    }

    /// <summary>Whether the view started the running session and still shows it.</summary>
    internal bool OwnsSession { get; private set; }

    /// <summary>Whether the link has the session service.</summary>
    internal bool IsConnected => _session != null;

    /// <summary>
    /// Subscribes to the session service and runs a start that waited for it. A second connection is ignored.
    /// </summary>
    internal void Connect(IGameSessionService session)
    {
        if (_session != null) return;

        _session = session;
        session.StateChanged += OnStateChanged;
        session.GeneratedMapReady += OnGeneratedMapReady;
        session.PlayerMovedWorld += OnPlayerMoved;
        session.VisibilityCellsUpdated += OnVisibilityUpdated;
        session.PathCellsUpdated += OnPathUpdated;
        session.EnemyDefeatedCell += OnEnemyDefeated;
        session.LootGenerated += OnLootGenerated;

        if (_pendingStart is not { } pending) return;

        ILog.Print("Processing pending session start...");
        _pendingStart = null;
        StartOwnSession(pending.MapSeed, pending.Abilities, pending.Generator);
    }

    /// <summary>Lets go of the session and unsubscribes from the session service.</summary>
    internal void Disconnect()
    {
        Release();
        if (_session == null) return;

        _session.StateChanged -= OnStateChanged;
        _session.GeneratedMapReady -= OnGeneratedMapReady;
        _session.PlayerMovedWorld -= OnPlayerMoved;
        _session.VisibilityCellsUpdated -= OnVisibilityUpdated;
        _session.PathCellsUpdated -= OnPathUpdated;
        _session.EnemyDefeatedCell -= OnEnemyDefeated;
        _session.LootGenerated -= OnLootGenerated;
        _session = null;
    }

    /// <summary>Starts a session for the view, or keeps the start until the service connects.</summary>
    internal void Start(CardSignature[] mapSeed, CardSignature[] abilities, IMapGenerator generator)
    {
        if (_session == null)
        {
            ILog.Print("GameSessionService not ready, storing pending session start...");
            _pendingStart = (mapSeed, abilities, generator);
            return;
        }

        StartOwnSession(mapSeed, abilities, generator);
    }

    /// <summary>Lets go of the session and drops a start still waiting for the service.</summary>
    internal void Reset()
    {
        Release();
        _pendingStart = null;
    }

    /// <summary>Reports the view's latest move shown, for a session the view owns.</summary>
    internal void ReportStepShown()
    {
        if (OwnsSession)
            _session?.ReportStepShown();
    }

    private void StartOwnSession(CardSignature[] mapSeed, CardSignature[] abilities, IMapGenerator generator)
    {
        var session = _session!;
        if (session.CurrentState != SessionState.WaitingForCards)
        {
            ILog.Error("Cannot start a session while another session runs");
            return;
        }

        var visibilityChecker = _view.PrepareForSession();
        OwnsSession = true;
        session.StartSession(mapSeed.ToList(), abilities.ToList(), generator, visibilityChecker);

        // A start the service refused leaves it waiting; the view then owns nothing.
        if (session.CurrentState == SessionState.WaitingForCards)
            OwnsSession = false;
    }

    private void Release()
    {
        OwnsSession = false;
        _view.OnSessionReleased();
    }

    private void OnStateChanged(SessionState state)
    {
        if (!OwnsSession) return;

        _view.OnStateChanged(state);
        if (state == SessionState.WaitingForCards)
            Release();
    }

    private void OnGeneratedMapReady(IGeneratedMap map)
    {
        if (OwnsSession) _view.OnMapReady(map);
    }

    private void OnPlayerMoved(Vector2 worldPosition)
    {
        if (OwnsSession) _view.OnPlayerMoved(worldPosition);
    }

    private void OnVisibilityUpdated(IReadOnlySet<int> seenCells, IReadOnlySet<int> visibleCells)
    {
        if (OwnsSession) _view.OnVisibilityUpdated(seenCells, visibleCells);
    }

    private void OnPathUpdated(IReadOnlyList<int> pathCells, int? targetCell)
    {
        if (OwnsSession) _view.OnPathUpdated(pathCells, targetCell);
    }

    private void OnEnemyDefeated(int cellId)
    {
        if (OwnsSession) _view.OnEnemyDefeated(cellId);
    }

    private void OnLootGenerated(List<CardSignature> loot)
    {
        if (OwnsSession) _view.OnLootGenerated(loot);
    }
}
