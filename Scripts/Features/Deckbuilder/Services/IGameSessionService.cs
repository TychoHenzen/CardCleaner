using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

// ReSharper disable once CheckNamespace

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

public enum SessionState
{
    WaitingForCards,
    GeneratingMap,
    Exploring,
    InCombat,
    GeneratingLoot,
    SessionComplete
}

public interface IGameSessionService
{
    SessionState CurrentState { get; }
    event Action<SessionState> StateChanged;
    event Action<SimpleMapData> MapGenerated;
    event Action<List<CardSignature>> LootGenerated;
    event Action<Vector2I> PlayerMoved;
    event Action<Vector2I> EnemyDefeated;
    event Action<IReadOnlySet<Vector2I>> VisitedTilesUpdated;
    event Action<IReadOnlySet<Vector2I>, IReadOnlySet<Vector2I>> VisibilityUpdated;
    event Action<IReadOnlyList<Vector2I>, Vector2I?> PathUpdated;

    void StartSession(CardSignature mapSeed, List<CardSignature> abilityCards);
    void AdvanceSession();
    void ResetSession();
}