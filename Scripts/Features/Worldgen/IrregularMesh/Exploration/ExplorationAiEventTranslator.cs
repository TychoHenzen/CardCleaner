using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Exploration;

/// <summary>
/// Subscribes to an ExplorationAI and re-raises its position-based events in terms of mesh cell ids.
/// </summary>
internal sealed class ExplorationAiEventTranslator
{
    private readonly ExplorationAI _explorationAI;
    private readonly IrregularMeshMapData _mapData;

    /// <summary>
    /// Raised with the AI's remaining path as cell ids.
    /// </summary>
    internal event Action<IReadOnlyList<int>>? PathUpdated;

    internal event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityUpdated;

    /// <summary>
    /// Raised with the cell of the spotted enemy; not raised when the position maps to no cell.
    /// </summary>
    internal event Action<int>? EnemySpotted;

    /// <summary>
    /// Raised with the cell of the encountered enemy, or null when the position maps to no cell.
    /// </summary>
    internal event Action<int?>? EnemyEncountered;

    internal ExplorationAiEventTranslator(ExplorationAI explorationAI, IrregularMeshMapData mapData)
    {
        _explorationAI = explorationAI;
        _mapData = mapData;

        _explorationAI.PathUpdated += OnPathUpdated;
        _explorationAI.VisibilityUpdated += OnVisibilityUpdated;
        _explorationAI.EnemySpotted += OnEnemySpotted;
        _explorationAI.EnemyEncountered += OnEnemyEncountered;
    }

    private void OnPathUpdated()
    {
        if (_explorationAI.CurrentPath != null)
        {
            PathUpdated?.Invoke(_explorationAI.CurrentPath.ToList());
        }
    }

    private void OnVisibilityUpdated(IReadOnlySet<int> seen, IReadOnlySet<int> visible)
    {
        VisibilityUpdated?.Invoke(seen, visible);
    }

    private void OnEnemySpotted(Vector2 enemyPos)
    {
        var cellId = _mapData.GetCellAtPosition(enemyPos);
        if (cellId.HasValue)
            EnemySpotted?.Invoke(cellId.Value);
    }

    private void OnEnemyEncountered(Vector2 enemyPos)
    {
        EnemyEncountered?.Invoke(_mapData.GetCellAtPosition(enemyPos));
    }
}
