using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// WFC topology over any cell graph given as one neighbor array per cell. Adjacency is the neighbor list
/// alone, so the window-neighbor defaults of <see cref="IWfcTopology"/> apply unchanged.
/// </summary>
public sealed class WfcNeighborListTopology : IWfcTopology
{
    private readonly WfcCellState[] _cells;

    /// <summary>
    /// Neighbor arrays indexed by cell ID, for non-alloc access.
    /// </summary>
    private readonly int[][] _neighbors;

    /// <summary>
    /// Maximum neighbor count across all cells.
    /// </summary>
    private readonly int _maxNeighborCount;

    /// <summary>
    /// Creates a topology with one cell per neighbor array, each starting with the same possible tiles.
    /// </summary>
    /// <param name="neighbors">Neighbor cell IDs for each cell. The array index is the cell ID.</param>
    /// <param name="initialTiles">Initial possible tiles for all cells.</param>
    public WfcNeighborListTopology(int[][] neighbors, IEnumerable<string> initialTiles)
    {
        var tileList = initialTiles.ToList();

        _cells = new WfcCellState[neighbors.Length];
        for (var i = 0; i < neighbors.Length; i++)
        {
            _cells[i] = new WfcCellState(tileList);
        }

        _neighbors = neighbors;
        _maxNeighborCount = 0;
        foreach (var cellNeighbors in neighbors)
        {
            _maxNeighborCount = Math.Max(_maxNeighborCount, cellNeighbors.Length);
        }
    }

    /// <inheritdoc />
    public int CellCount => _cells.Length;

    /// <inheritdoc />
    public WfcCellState GetCell(int cellId) => _cells[cellId];

    /// <inheritdoc />
    public bool IsValidCell(int cellId) => cellId >= 0 && cellId < _cells.Length;

    /// <inheritdoc />
    public IEnumerable<int> GetAllCellIds()
    {
        for (var i = 0; i < _cells.Length; i++)
        {
            yield return i;
        }
    }

    /// <inheritdoc />
    public IEnumerable<int> GetNeighbors(int cellId)
    {
        if (cellId < 0 || cellId >= _neighbors.Length)
            yield break;

        foreach (var neighbor in _neighbors[cellId])
        {
            yield return neighbor;
        }
    }

    /// <inheritdoc />
    public int GetNeighborsNonAlloc(int cellId, Span<int> output)
    {
        if (cellId < 0 || cellId >= _neighbors.Length)
            return 0;

        var neighbors = _neighbors[cellId];
        var count = Math.Min(neighbors.Length, output.Length);

        for (var i = 0; i < count; i++)
        {
            output[i] = neighbors[i];
        }

        return count;
    }

    /// <inheritdoc />
    public int MaxNeighborCount => _maxNeighborCount;

    /// <inheritdoc />
    public bool HasCollapsedNeighbor(int cellId)
    {
        foreach (var neighborId in GetNeighbors(cellId))
        {
            if (_cells[neighborId].IsCollapsed())
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public string? GetCollapsedTileAt(int cellId)
    {
        if (cellId < 0 || cellId >= _cells.Length)
            return null;

        var cell = _cells[cellId];
        return cell.IsCollapsed() ? cell.GetCollapsedTile() : null;
    }

    /// <inheritdoc />
    public bool IsFullyCollapsed()
    {
        foreach (var cell in _cells)
        {
            if (!cell.IsCollapsed())
                return false;
        }
        return true;
    }

    /// <inheritdoc />
    public bool HasContradiction()
    {
        foreach (var cell in _cells)
        {
            if (cell.IsContradiction())
                return true;
        }
        return false;
    }
}
