using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// 2D grid of WFC cells. Manages cell access and neighbor enumeration.
/// Cell IDs are linearized as (y * width + x) for flat iteration.
/// Implements IWfcTopology for use with topology-agnostic WFC components.
/// </summary>
public class WfcGrid : IWfcTopology
{
    private readonly WfcCellState[,] _cells;
    private readonly WfcGridGeometry _geometry;

    /// <summary>
    /// Grid width in cells.
    /// </summary>
    public int Width => _geometry.Width;

    /// <summary>
    /// Grid height in cells.
    /// </summary>
    public int Height => _geometry.Height;

    /// <summary>
    /// Creates a grid where all cells start with the same possible tiles.
    /// </summary>
    public WfcGrid(int width, int height, IEnumerable<string> initialTiles)
    {
        _geometry = new WfcGridGeometry(width, height);
        _cells = new WfcCellState[height, width];

        var tileList = new List<string>(initialTiles);
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                _cells[y, x] = new WfcCellState(tileList);
            }
        }
    }

    /// <summary>
    /// Gets the cell at the given position.
    /// </summary>
    public WfcCellState GetCell(int x, int y) => _cells[y, x];

    /// <summary>
    /// Gets the cell at the given position.
    /// </summary>
    public WfcCellState GetCell(Vector2I pos) => _cells[pos.Y, pos.X];

    /// <summary>
    /// Checks if a position is within grid bounds.
    /// </summary>
    public bool IsInBounds(int x, int y) => _geometry.IsInBounds(x, y);

    /// <summary>
    /// Checks if a position is within grid bounds.
    /// </summary>
    public bool IsInBounds(Vector2I pos) => IsInBounds(pos.X, pos.Y);

    /// <summary>
    /// Enumerates the 4-directional neighbors of a position (N, E, S, W).
    /// Used for adjacency rule checking where only directly adjacent tiles matter.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors(int x, int y) => _geometry.EnumerateNeighbors4(x, y);

    /// <summary>
    /// Enumerates the 4-directional neighbors of a position.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors(Vector2I pos) => GetNeighbors(pos.X, pos.Y);

    /// <summary>
    /// Enumerates all 8 neighbors of a position (N, NE, E, SE, S, SW, W, NW).
    /// Used for 2x2 window constraint propagation where diagonal cells share windows.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors8(int x, int y) => _geometry.EnumerateNeighbors8(x, y);

    /// <summary>
    /// Enumerates all 8 neighbors of a position.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors8(Vector2I pos) => GetNeighbors8(pos.X, pos.Y);

    /// <summary>
    /// Writes 4-directional neighbors into a span and returns how many were written.
    /// </summary>
    public int GetNeighborsNonAlloc(Vector2I pos, Span<Vector2I> output) =>
        _geometry.CollectNeighbors4(pos, output);

    /// <summary>
    /// Writes 8-directional neighbors into a span and returns how many were written.
    /// </summary>
    public int GetNeighbors8NonAlloc(Vector2I pos, Span<Vector2I> output) =>
        _geometry.CollectNeighbors8(pos, output);

    /// <summary>
    /// Checks if all cells have collapsed to a single tile.
    /// </summary>
    public bool IsFullyCollapsed()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (!_cells[y, x].IsCollapsed())
                    return false;
            }
        }
        return true;
    }

    /// <summary>
    /// Checks if any cell is in contradiction state (no valid options).
    /// </summary>
    public bool HasContradiction()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (_cells[y, x].IsContradiction())
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a position has at least one collapsed neighbor.
    /// </summary>
    public bool HasCollapsedNeighbor(Vector2I pos)
    {
        foreach (var neighbor in GetNeighbors(pos))
        {
            if (GetCell(neighbor).IsCollapsed())
                return true;
        }
        return false;
    }

    /// <summary>
    /// Finds lowest weighted-entropy cell among frontier cells (adjacent to collapsed).
    /// Only computes expensive entropy for frontier cells, dramatically reducing work.
    /// Falls back to any uncollapsed cell only when no frontier exists (start of generation).
    /// </summary>
    /// <param name="getWeightsAt">Function that returns tile weights for a position</param>
    /// <param name="rng">Random number generator for tiebreaking</param>
    /// <returns>Position of cell with lowest weighted entropy, or null if all collapsed</returns>
    public Vector2I? GetLowestEntropyCellWeighted(
        Func<Vector2I, IReadOnlyDictionary<string, float>> getWeightsAt,
        RandomNumberGenerator rng) =>
        WfcFrontierEntropySelector.Select(this, getWeightsAt, rng);

    /// <summary>
    /// Enumerates all cell positions.
    /// </summary>
    public IEnumerable<Vector2I> GetAllPositions()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                yield return new Vector2I(x, y);
            }
        }
    }

    /// <summary>
    /// Gets the collapsed tile ID at a position, or null if not collapsed.
    /// </summary>
    public string? GetCollapsedTileAt(Vector2I pos)
    {
        var cell = _cells[pos.Y, pos.X];
        return cell.IsCollapsed() ? cell.GetCollapsedTile() : null;
    }

    /// <summary>
    /// Total number of cells in the grid.
    /// </summary>
    public int CellCount => _geometry.CellCount;

    /// <summary>
    /// Gets the cell at the given linearized ID.
    /// </summary>
    public WfcCellState GetCell(int cellId)
    {
        var pos = CellIdToPosition(cellId);
        return _cells[pos.Y, pos.X];
    }

    /// <summary>
    /// Checks if a cell ID is valid.
    /// </summary>
    public bool IsValidCell(int cellId) => cellId >= 0 && cellId < CellCount;

    /// <summary>
    /// Enumerates the 4-directional neighbors of a cell ID.
    /// Use this for connectivity/movement (walking is 4-way).
    /// </summary>
    public IEnumerable<int> GetNeighbors4(int cellId)
    {
        var pos = CellIdToPosition(cellId);
        foreach (var neighborPos in GetNeighbors(pos))
        {
            yield return PositionToCellId(neighborPos);
        }
    }

    /// <summary>
    /// Enumerates the edge-sharing neighbors of a cell ID (IWfcTopology implementation).
    /// Adjacency rules and neighbor-based constraints apply to these cells only.
    /// </summary>
    IEnumerable<int> IWfcTopology.GetNeighbors(int cellId) => GetNeighbors4(cellId);

    /// <summary>
    /// Gets 4-way neighbors into a pre-allocated span (IWfcTopology implementation).
    /// </summary>
    int IWfcTopology.GetNeighborsNonAlloc(int cellId, Span<int> output) =>
        _geometry.CollectNeighborIds4(cellId, output);

    /// <summary>
    /// Enumerates the 8 cells sharing a 2x2 window with a cell (IWfcTopology implementation).
    /// Used to find cells to re-evaluate after a collapse and by the auto-tile gap rule.
    /// </summary>
    IEnumerable<int> IWfcTopology.GetWindowNeighbors(int cellId) => GetNeighbors8(cellId);

    /// <summary>
    /// Gets 8-way window neighbors into a pre-allocated span (IWfcTopology implementation).
    /// </summary>
    int IWfcTopology.GetWindowNeighborsNonAlloc(int cellId, Span<int> output) =>
        _geometry.CollectNeighborIds8(cellId, output);

    /// <summary>
    /// Maximum neighbors is 8 for rectangular grid (IWfcTopology implementation).
    /// </summary>
    int IWfcTopology.MaxNeighborCount => 8;

    /// <summary>
    /// Enumerates all cell IDs in the grid.
    /// </summary>
    public IEnumerable<int> GetAllCellIds()
    {
        for (var i = 0; i < CellCount; i++)
        {
            yield return i;
        }
    }

    /// <summary>
    /// Checks if a cell ID has at least one collapsed neighbor.
    /// </summary>
    public bool HasCollapsedNeighbor(int cellId) => HasCollapsedNeighbor(CellIdToPosition(cellId));

    /// <summary>
    /// Gets the collapsed tile ID at a cell ID, or null if not collapsed.
    /// </summary>
    public string? GetCollapsedTileAt(int cellId) => GetCollapsedTileAt(CellIdToPosition(cellId));

    /// <summary>
    /// Enumerates the 8-directional neighbors of a cell ID.
    /// </summary>
    public IEnumerable<int> GetNeighbors8(int cellId)
    {
        var pos = CellIdToPosition(cellId);
        foreach (var neighborPos in GetNeighbors8(pos))
        {
            yield return PositionToCellId(neighborPos);
        }
    }

    /// <summary>
    /// Converts a cell ID to a grid position.
    /// </summary>
    public Vector2I CellIdToPosition(int cellId) => _geometry.CellIdToPosition(cellId);

    /// <summary>
    /// Converts a grid position to a cell ID.
    /// </summary>
    public int PositionToCellId(Vector2I position) => _geometry.PositionToCellId(position);

    /// <summary>
    /// Creates a deep copy of this grid (for backtracking support).
    /// </summary>
    public WfcGrid Clone()
    {
        var clone = new WfcGrid(Width, Height, new List<string>());
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                clone._cells[y, x] = new WfcCellState(_cells[y, x]);
            }
        }
        return clone;
    }
}
