using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

/// <summary>
/// Adapts cell-based events to grid-based (Vector2I) events for backward compatibility.
/// When a RegularGridMapData is available, emits both cell and grid events.
/// When only cell data is available (irregular mesh), emits only cell events.
/// </summary>
public class GridCellEventAdapter
{
    private RegularGridMapData? _gridMapData;

    /// <summary>
    /// Sets the grid map data for coordinate translation.
    /// Set to null when using non-grid map generators.
    /// </summary>
    public void SetGridMapData(RegularGridMapData? gridMapData)
    {
        _gridMapData = gridMapData;
    }

    /// <summary>
    /// Whether grid-based events will be emitted (grid map data is available).
    /// </summary>
    public bool HasGridData => _gridMapData != null;

    /// <summary>
    /// Converts a world position to grid position if grid data is available.
    /// </summary>
    public Vector2I? WorldToGrid(Vector2 worldPos)
    {
        return _gridMapData?.WorldToGrid(worldPos);
    }

    /// <summary>
    /// Gets the grid position for a cell ID if grid data is available.
    /// </summary>
    public Vector2I? GetGridPosition(int cellId)
    {
        return _gridMapData?.CellIdToPosition(cellId);
    }

    /// <summary>
    /// Emits player moved events in both coordinate systems.
    /// </summary>
    public void EmitPlayerMoved(
        Vector2 worldPos,
        Action<Vector2>? worldEvent,
        Action<Vector2I>? gridEvent)
    {
        worldEvent?.Invoke(worldPos);

        if (_gridMapData != null)
        {
            var gridPos = _gridMapData.WorldToGrid(worldPos);
            gridEvent?.Invoke(gridPos);
        }
    }

    /// <summary>
    /// Emits visited cells events in both coordinate systems.
    /// </summary>
    public void EmitVisitedCells(
        IReadOnlySet<int> cellIds,
        Action<IReadOnlySet<int>>? cellEvent,
        Action<IReadOnlySet<Vector2I>>? gridEvent)
    {
        cellEvent?.Invoke(cellIds);

        if (_gridMapData != null)
        {
            var gridPositions = new HashSet<Vector2I>(
                cellIds.Select(id => _gridMapData.CellIdToPosition(id)));
            gridEvent?.Invoke(gridPositions);
        }
    }

    /// <summary>
    /// Emits visibility events in both coordinate systems.
    /// </summary>
    public void EmitVisibility(
        IReadOnlySet<int> seenCells,
        IReadOnlySet<int> visibleCells,
        Action<IReadOnlySet<int>, IReadOnlySet<int>>? cellEvent,
        Action<IReadOnlySet<Vector2I>, IReadOnlySet<Vector2I>>? gridEvent)
    {
        cellEvent?.Invoke(seenCells, visibleCells);

        if (_gridMapData != null)
        {
            var seenPositions = new HashSet<Vector2I>(
                seenCells.Select(id => _gridMapData.CellIdToPosition(id)));
            var visiblePositions = new HashSet<Vector2I>(
                visibleCells.Select(id => _gridMapData.CellIdToPosition(id)));
            gridEvent?.Invoke(seenPositions, visiblePositions);
        }
    }

    /// <summary>
    /// Emits path events in both coordinate systems.
    /// </summary>
    public void EmitPath(
        IReadOnlyList<int> pathCells,
        int? targetCell,
        Action<IReadOnlyList<int>, int?>? cellEvent,
        Action<IReadOnlyList<Vector2I>, Vector2I?>? gridEvent)
    {
        cellEvent?.Invoke(pathCells, targetCell);

        if (_gridMapData != null)
        {
            var pathPositions = pathCells
                .Select(id => _gridMapData.CellIdToPosition(id))
                .ToList();

            var targetPosition = targetCell.HasValue
                ? _gridMapData.CellIdToPosition(targetCell.Value)
                : (Vector2I?)null;

            gridEvent?.Invoke(pathPositions, targetPosition);
        }
    }

    /// <summary>
    /// Emits enemy defeated events in both coordinate systems.
    /// </summary>
    public void EmitEnemyDefeated(
        int cellId,
        Action<int>? cellEvent,
        Action<Vector2I>? gridEvent)
    {
        cellEvent?.Invoke(cellId);

        if (_gridMapData != null)
        {
            var gridPos = _gridMapData.CellIdToPosition(cellId);
            gridEvent?.Invoke(gridPos);
        }
    }
}
