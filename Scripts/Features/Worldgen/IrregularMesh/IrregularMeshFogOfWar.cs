using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Per-quad fog of war for irregular mesh terrain. It computes no visibility: the session's exploration hands it the
/// seen and the visible cells (<see cref="UpdateVisibility"/>), like the grid map's fog. A cell it has shown once stays
/// at least revealed.
/// </summary>
public class IrregularMeshFogOfWar : IPassiveFogOfWar
{
    private readonly Dictionary<int, FogState> _fogStates = new();
    private readonly HashSet<int> _currentlyVisible = new();

    public IrregularMeshFogOfWar(IrregularMeshMapData mapData)
    {
        for (var i = 0; i < mapData.CellCount; i++)
            _fogStates[i] = FogState.Hidden;
    }

    /// <summary>
    /// Currently visible cell IDs.
    /// </summary>
    public IReadOnlySet<int> CurrentlyVisibleCells => _currentlyVisible;

    /// <summary>
    /// All cells that have ever been seen (revealed or visible).
    /// </summary>
    public IReadOnlySet<int> SeenCells => _fogStates
        .Where(kv => kv.Value != FogState.Hidden)
        .Select(kv => kv.Key)
        .ToHashSet();

    /// <summary>
    /// Raised when visibility state changes.
    /// Parameters: (changedCellIds)
    /// </summary>
    public event Action<IReadOnlySet<int>>? VisibilityChanged;

    public FogState GetFogState(int cellId) =>
        _fogStates.TryGetValue(cellId, out var state) ? state : FogState.Hidden;

    public bool IsVisible(int cellId) => GetFogState(cellId) == FogState.Visible;

    public bool HasBeenSeen(int cellId) => GetFogState(cellId) != FogState.Hidden;

    /// <summary>
    /// Shows the visible cells and reveals the seen ones. A cell missing from a later seen set stays revealed: each
    /// resume after a combat starts a new explorer, whose seen set starts empty. Cells outside the map are ignored.
    /// </summary>
    public void UpdateVisibility(IReadOnlySet<int> seenCellIds, IReadOnlySet<int> visibleCellIds)
    {
        var changedCells = new HashSet<int>();

        foreach (var cellId in _currentlyVisible)
        {
            if (!visibleCellIds.Contains(cellId))
                SetState(cellId, FogState.Revealed, changedCells);
        }

        foreach (var cellId in seenCellIds)
        {
            if (!visibleCellIds.Contains(cellId) && GetFogState(cellId) == FogState.Hidden)
                SetState(cellId, FogState.Revealed, changedCells);
        }

        _currentlyVisible.Clear();
        foreach (var cellId in visibleCellIds)
        {
            if (!_fogStates.ContainsKey(cellId))
                continue;

            _currentlyVisible.Add(cellId);
            SetState(cellId, FogState.Visible, changedCells);
        }

        RaiseChanged(changedCells);
    }

    /// <summary>
    /// Reveal all cells (disable fog of war).
    /// </summary>
    public void RevealAll()
    {
        var changedCells = new HashSet<int>();
        foreach (var cellId in _fogStates.Keys.ToList())
        {
            _currentlyVisible.Add(cellId);
            SetState(cellId, FogState.Visible, changedCells);
        }

        RaiseChanged(changedCells);
    }

    /// <summary>
    /// Reset all cells to hidden.
    /// </summary>
    public void Reset()
    {
        var changedCells = new HashSet<int>();
        foreach (var cellId in _fogStates.Keys.ToList())
            SetState(cellId, FogState.Hidden, changedCells);

        _currentlyVisible.Clear();
        RaiseChanged(changedCells);
    }

    /// <summary>
    /// Get all cells with a specific fog state.
    /// </summary>
    public IEnumerable<int> GetCellsWithState(FogState state)
    {
        return _fogStates.Where(kv => kv.Value == state).Select(kv => kv.Key);
    }

    /// <summary>
    /// Get visibility statistics.
    /// </summary>
    internal FogStatistics GetStatistics()
    {
        int hidden = 0, revealed = 0, visible = 0;

        foreach (var state in _fogStates.Values)
        {
            switch (state)
            {
                case FogState.Hidden: hidden++; break;
                case FogState.Revealed: revealed++; break;
                case FogState.Visible: visible++; break;
            }
        }

        return new FogStatistics(hidden, revealed, visible);
    }

    private void SetState(int cellId, FogState state, HashSet<int> changedCells)
    {
        if (!_fogStates.TryGetValue(cellId, out var previous) || previous == state)
            return;

        _fogStates[cellId] = state;
        changedCells.Add(cellId);
    }

    private void RaiseChanged(HashSet<int> changedCells)
    {
        if (changedCells.Count > 0)
            VisibilityChanged?.Invoke(changedCells);
    }
}
