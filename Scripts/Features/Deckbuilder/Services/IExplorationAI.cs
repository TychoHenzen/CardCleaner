using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>Autonomous exploration of one map: the steps it takes and the events it raises.</summary>
public interface IExplorationAI
{
    bool HasFoundEnemy { get; }
    IReadOnlyList<int> CurrentPath { get; }
    int? CurrentTargetCell { get; }

    event Action<Vector2>? PlayerMoved;
    event Action<Vector2>? EnemyEncountered;
    event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityUpdated;
    event Action? PathUpdated;

    /// <summary>Takes one exploration step. Returns true while the exploration continues.</summary>
    bool StepExploration();
}
