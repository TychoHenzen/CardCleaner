using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>Runs autonomous exploration for a session and raises its events in cell ids and world positions.</summary>
internal interface IExplorationSession
{
    int MoveCount { get; }
    IVisibilityChecker? VisibilityChecker { get; set; }

    event Action<Vector2>? PlayerMovedWorld;
    event Action<int>? EnemyDefeatedCell;
    event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;
    event Action<IReadOnlyList<int>, int?>? PathCellsUpdated;

    bool Start();
    ExplorationStepOutcome Step();
    void ResolveDefeatedEnemy();
    void Clear();
}
