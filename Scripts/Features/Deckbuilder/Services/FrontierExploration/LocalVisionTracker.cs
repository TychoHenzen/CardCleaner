using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.FrontierExploration;

/// <summary>
/// Tracks seen and currently visible cells when no external fog of war system is provided.
/// </summary>
internal sealed class LocalVisionTracker
{
    private const float DefaultCellSize = 16f;

    private readonly HashSet<int> _seenCells = new();
    private readonly HashSet<int> _currentlyVisibleCells = new();
    private readonly IMapData _mapData;
    private readonly IVisibilityChecker _visibilityChecker;
    private readonly int _visionRange;

    internal LocalVisionTracker(IMapData mapData, IVisibilityChecker visibilityChecker, int visionRange)
    {
        _mapData = mapData;
        _visibilityChecker = visibilityChecker;
        _visionRange = visionRange;
    }

    internal IReadOnlySet<int> SeenCells => _seenCells;

    internal IReadOnlySet<int> CurrentlyVisibleCells => _currentlyVisibleCells;

    /// <summary>
    /// Recalculate the currently visible cells from the given position.
    /// </summary>
    internal void Update(int currentCellId)
    {
        _currentlyVisibleCells.Clear();

        _seenCells.Add(currentCellId);
        _currentlyVisibleCells.Add(currentCellId);

        var currentPos = _mapData.GetCellCenter(currentCellId);
        var rangeInWorldUnits = _visionRange * EstimateCellSize();

        foreach (var targetCellId in _mapData.GetCellsInRadius(currentPos, rangeInWorldUnits))
        {
            if (targetCellId == currentCellId)
                continue;

            // Skip if outside vision range (circular check)
            if (currentPos.DistanceTo(_mapData.GetCellCenter(targetCellId)) > rangeInWorldUnits)
                continue;

            if (!_visibilityChecker.CanSee(currentCellId, targetCellId, _mapData))
                continue;

            _seenCells.Add(targetCellId);
            _currentlyVisibleCells.Add(targetCellId);
        }
    }

    /// <summary>
    /// Estimate average cell size for vision range calculation.
    /// </summary>
    private float EstimateCellSize()
    {
        if (_mapData.CellCount == 0)
            return DefaultCellSize;

        // Sample first cell to estimate size
        return Mathf.Sqrt(_mapData.GetCellArea(0));
    }
}
