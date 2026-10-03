using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Manages per-quad fog of war state for irregular mesh terrain.
/// Tracks which quads have been seen, are currently visible, or are hidden.
/// Implements IActiveFogOfWar for active visibility calculation from observer position.
/// </summary>
public class IrregularMeshFogOfWar : IActiveFogOfWar
{
    private readonly IrregularMeshMapData _mapData;
    private readonly Dictionary<int, FogState> _fogStates = new();
    private readonly HashSet<int> _currentlyVisible = new();
    private readonly HashSet<int> _previouslyVisible = new();

    private IVisibilityChecker? _visibilityChecker;
    private float _visionRange = 5f;

    /// <summary>
    /// The vision range in world units.
    /// </summary>
    public float VisionRange
    {
        get => _visionRange;
        set => _visionRange = Mathf.Max(0f, value);
    }

    /// <summary>
    /// Number of cells per vision range unit for range calculation.
    /// </summary>
    public float CellsPerUnit { get; set; } = 1f;

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

    /// <summary>
    /// Raised when a cell is revealed for the first time.
    /// Parameters: (cellId)
    /// </summary>
    public event Action<int>? CellRevealed;

    public IrregularMeshFogOfWar(IrregularMeshMapData mapData, IVisibilityChecker? visibilityChecker = null)
    {
        _mapData = mapData;
        _visibilityChecker = visibilityChecker;

        // Initialize all cells as hidden
        for (int i = 0; i < mapData.CellCount; i++)
        {
            _fogStates[i] = FogState.Hidden;
        }
    }

    /// <summary>
    /// Set the visibility checker for line-of-sight calculations.
    /// </summary>
    public void SetVisibilityChecker(IVisibilityChecker checker)
    {
        _visibilityChecker = checker;
    }

    /// <summary>
    /// Get the fog state for a cell.
    /// </summary>
    public FogState GetFogState(int cellId)
    {
        return _fogStates.TryGetValue(cellId, out var state) ? state : FogState.Hidden;
    }

    /// <summary>
    /// Check if a cell is currently visible.
    /// </summary>
    public bool IsVisible(int cellId) => GetFogState(cellId) == FogState.Visible;

    /// <summary>
    /// Check if a cell has been seen (revealed or visible).
    /// </summary>
    public bool HasBeenSeen(int cellId) => GetFogState(cellId) != FogState.Hidden;

    /// <summary>
    /// Update visibility from an observer position.
    /// </summary>
    /// <param name="observerCellId">The cell ID of the observer.</param>
    public void UpdateVisibility(int observerCellId)
    {
        var changedCells = new HashSet<int>();

        // Store previous visibility state
        _previouslyVisible.Clear();
        foreach (var cellId in _currentlyVisible)
        {
            _previouslyVisible.Add(cellId);
        }
        _currentlyVisible.Clear();

        // Get observer world position
        var observerPos = _mapData.GetCellCenter(observerCellId);

        // Find all cells within vision range
        var cellsInRange = _mapData.GetCellsInRadius(observerPos, _visionRange * CellsPerUnit);

        foreach (var cellId in cellsInRange)
        {
            // Check line of sight
            bool canSee = CanSeeCell(observerCellId, cellId, observerPos);

            if (canSee)
            {
                _currentlyVisible.Add(cellId);

                var previousState = _fogStates[cellId];
                _fogStates[cellId] = FogState.Visible;

                if (previousState != FogState.Visible)
                {
                    changedCells.Add(cellId);

                    if (previousState == FogState.Hidden)
                    {
                        CellRevealed?.Invoke(cellId);
                    }
                }
            }
        }

        // Mark previously visible cells as revealed (if not currently visible)
        foreach (var cellId in _previouslyVisible)
        {
            if (!_currentlyVisible.Contains(cellId))
            {
                if (_fogStates[cellId] == FogState.Visible)
                {
                    _fogStates[cellId] = FogState.Revealed;
                    changedCells.Add(cellId);
                }
            }
        }

        if (changedCells.Count > 0)
        {
            VisibilityChanged?.Invoke(changedCells);
        }
    }

    /// <summary>
    /// Update visibility from a world position.
    /// </summary>
    /// <param name="worldPos">The world position of the observer.</param>
    public void UpdateVisibilityFromPosition(Vector2 worldPos)
    {
        var cellId = _mapData.GetCellAtPosition(worldPos);
        if (cellId.HasValue)
        {
            UpdateVisibility(cellId.Value);
        }
    }

    /// <summary>
    /// Reveal a specific cell (mark as seen without requiring line of sight).
    /// </summary>
    public void RevealCell(int cellId)
    {
        if (!_mapData.IsValidCell(cellId))
            return;

        var previousState = _fogStates[cellId];
        if (previousState == FogState.Hidden)
        {
            _fogStates[cellId] = FogState.Revealed;
            CellRevealed?.Invoke(cellId);
            VisibilityChanged?.Invoke(new HashSet<int> { cellId });
        }
    }

    /// <summary>
    /// Reveal all cells (disable fog of war).
    /// </summary>
    public void RevealAll()
    {
        var changedCells = new HashSet<int>();

        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (_fogStates[i] == FogState.Hidden)
            {
                _fogStates[i] = FogState.Revealed;
                changedCells.Add(i);
                CellRevealed?.Invoke(i);
            }
        }

        if (changedCells.Count > 0)
        {
            VisibilityChanged?.Invoke(changedCells);
        }
    }

    /// <summary>
    /// Reset all cells to hidden.
    /// </summary>
    public void Reset()
    {
        var changedCells = new HashSet<int>();

        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (_fogStates[i] != FogState.Hidden)
            {
                _fogStates[i] = FogState.Hidden;
                changedCells.Add(i);
            }
        }

        _currentlyVisible.Clear();
        _previouslyVisible.Clear();

        if (changedCells.Count > 0)
        {
            VisibilityChanged?.Invoke(changedCells);
        }
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

    private bool CanSeeCell(int fromCellId, int toCellId, Vector2 fromPos)
    {
        // Same cell is always visible
        if (fromCellId == toCellId)
            return true;

        // Check distance
        var toPos = _mapData.GetCellCenter(toCellId);
        var distance = fromPos.DistanceTo(toPos);

        if (distance > _visionRange * CellsPerUnit)
            return false;

        // Check line of sight if we have a visibility checker
        if (_visibilityChecker != null)
        {
            return _visibilityChecker.CanSee(fromCellId, toCellId, _mapData);
        }

        // No visibility checker - assume visible if in range
        return true;
    }
}
