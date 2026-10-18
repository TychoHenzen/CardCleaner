using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Godot;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Runs autonomous exploration for a session and raises its events with cell ids and world positions.
/// </summary>
[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords",
    Justification = "Step is the exploration verb of the session API that SessionFlow calls; " +
                    "C# is the only consumer.")]
public interface IExplorationSession
{
    /// <summary>Moves the player has made since the session's map was cleared.</summary>
    int MoveCount { get; }

    /// <summary>
    /// The line-of-sight checker the session's view handed over, or null for the registered or simple one.
    /// </summary>
    IVisibilityChecker? VisibilityChecker { get; set; }

    event Action<Vector2>? PlayerMovedWorld;

    event Action<int>? EnemyDefeatedCell;

    event Action<IReadOnlySet<int>>? VisitedCellsUpdated;

    event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;

    event Action<IReadOnlyList<int>, int?>? PathCellsUpdated;

    /// <summary>Starts exploring the current map. Returns false when there is no map to explore.</summary>
    bool Start();

    ExplorationStepOutcome Step();

    /// <summary>Moves the player onto the defeated enemy's cell and removes the enemy from the map.</summary>
    void ResolveDefeatedEnemy();

    void Clear();
}
