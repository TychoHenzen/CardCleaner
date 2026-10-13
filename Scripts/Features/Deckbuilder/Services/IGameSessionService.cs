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

    /// <summary>Raised after every move with the player's new world position, on both map kinds.</summary>
    event Action<Vector2> PlayerMovedWorld;

    /// <summary>Raised with the cell of every defeated enemy, on both map kinds.</summary>
    event Action<int> EnemyDefeatedCell;

    /// <summary>Raised with every cell the player has visited, after every move, on both map kinds.</summary>
    event Action<IReadOnlySet<int>> VisitedCellsUpdated;

    /// <summary>Raised with the cells of the current path and its target cell, on both map kinds.</summary>
    event Action<IReadOnlyList<int>, int?> PathCellsUpdated;

    /// <summary>
    /// Raised with the seen and the currently visible cells after every vision update, on both map kinds.
    /// </summary>
    event Action<IReadOnlySet<int>, IReadOnlySet<int>> VisibilityCellsUpdated;

    /// <summary>
    /// Raised for every generated map; <see cref="MapGenerated"/> is raised before it for a grid map only.
    /// </summary>
    event Action<IGeneratedMap> GeneratedMapReady;

    /// <summary>
    /// The current map from the active generator, or null before generation.
    /// </summary>
    IGeneratedMap? CurrentGeneratedMap { get; }

    /// <summary>
    /// Starts a session that generates its map with <paramref name="mapGenerator"/>. Exploration sees with
    /// <paramref name="visibilityChecker"/>, or with the registered or the simple checker when it is null.
    /// </summary>
    void StartSession(
        List<CardSignature> mapSeeds,
        List<CardSignature> abilityCards,
        IMapGenerator mapGenerator,
        IVisibilityChecker? visibilityChecker = null);
    void AdvanceSession();
    void ResetSession();

    /// <summary>
    /// Reports that the view has shown the player's latest move. After a move, exploration waits for this report
    /// before its next step, so the view sets the pace: report at once to keep the session's own step delay.
    /// </summary>
    void ReportStepShown();
}