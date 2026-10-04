using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

// ReSharper disable once CheckNamespace

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

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
    event Action<float>? ProgressUpdated;

    /// <summary>
    /// Raised for every generated map, including maps from a custom generator that skip <see cref="MapGenerated"/>.
    /// </summary>
    event Action<IGeneratedMap> GeneratedMapReady;

    /// <summary>
    /// The current map from the active generator, or null before generation.
    /// </summary>
    IGeneratedMap? CurrentGeneratedMap { get; }

    void StartSession(List<CardSignature> mapSeeds, List<CardSignature> abilityCards);
    void AdvanceSession();
    void ResetSession();

    /// <summary>
    /// Sets a custom map generator. Pass null to use the default generator.
    /// </summary>
    void SetMapGenerator(IMapGenerator? generator);
}