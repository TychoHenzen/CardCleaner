using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Controllers;

/// <summary>
/// A session service that only reports the states a test hands it. A start is recorded and reported as
/// <see cref="SessionState.GeneratingMap"/>; nothing generates.
/// </summary>
public sealed class FakeGameSession : IGameSessionService
{
    public SessionState CurrentState { get; private set; } = SessionState.WaitingForCards;

    /// <summary>How often a session was started.</summary>
    public int StartCount { get; private set; }

    /// <summary>The generator the last start handed over.</summary>
    public IMapGenerator? LastGenerator { get; private set; }

    public event Action<SessionState>? StateChanged;
#pragma warning disable CS0067 // The controllers under test never need these raised.
    public event Action<SimpleMapData>? MapGenerated;
    public event Action<List<CardSignature>>? LootGenerated;
    public event Action<Vector2I>? PlayerMoved;
    public event Action<Vector2>? PlayerMovedWorld;
    public event Action<Vector2I>? EnemyDefeated;
    public event Action<int>? EnemyDefeatedCell;
    public event Action<IReadOnlySet<Vector2I>>? VisitedTilesUpdated;
    public event Action<IReadOnlySet<Vector2I>, IReadOnlySet<Vector2I>>? VisibilityUpdated;
    public event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;
    public event Action<IReadOnlyList<Vector2I>, Vector2I?>? PathUpdated;
    public event Action<float>? ProgressUpdated;
    public event Action<IGeneratedMap>? GeneratedMapReady;
#pragma warning restore CS0067
    public IGeneratedMap? CurrentGeneratedMap => null;

    public void Report(SessionState state)
    {
        CurrentState = state;
        StateChanged?.Invoke(state);
    }

    public void StartSession(
        List<CardSignature> mapSeeds,
        List<CardSignature> abilityCards,
        IMapGenerator mapGenerator,
        IVisibilityChecker? visibilityChecker = null)
    {
        StartCount++;
        LastGenerator = mapGenerator;
        Report(SessionState.GeneratingMap);
    }

    public void AdvanceSession() { }
    public void ResetSession() { }
    public void ReportStepShown() { }
}
