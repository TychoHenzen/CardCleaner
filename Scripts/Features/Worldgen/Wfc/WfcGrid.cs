using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// 2D grid of WFC cells. Manages cell access and neighbor enumeration.
/// </summary>
public class WfcGrid
{
    // Pre-allocated neighbor offsets for avoiding repeated allocation in hot paths
    private static readonly Vector2I[] Neighbors4Offsets =
    {
        new(0, -1),  // North
        new(1, 0),   // East
        new(0, 1),   // South
        new(-1, 0)   // West
    };

    private static readonly Vector2I[] Neighbors8Offsets =
    {
        new(0, -1),   // North
        new(1, -1),   // North-East
        new(1, 0),    // East
        new(1, 1),    // South-East
        new(0, 1),    // South
        new(-1, 1),   // South-West
        new(-1, 0),   // West
        new(-1, -1)   // North-West
    };

    private readonly WfcCellState[,] _cells;
    private readonly int _width;
    private readonly int _height;

    /// <summary>
    /// Grid width in cells.
    /// </summary>
    public int Width => _width;

    /// <summary>
    /// Grid height in cells.
    /// </summary>
    public int Height => _height;

    /// <summary>
    /// Creates a grid where all cells start with the same possible tiles.
    /// </summary>
    public WfcGrid(int width, int height, IEnumerable<string> initialTiles)
    {
        _width = width;
        _height = height;
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
    public bool IsInBounds(int x, int y) => x >= 0 && x < _width && y >= 0 && y < _height;

    /// <summary>
    /// Checks if a position is within grid bounds.
    /// </summary>
    public bool IsInBounds(Vector2I pos) => IsInBounds(pos.X, pos.Y);

    /// <summary>
    /// Enumerates the 4-directional neighbors of a position (N, E, S, W).
    /// Used for adjacency rule checking where only directly adjacent tiles matter.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors(int x, int y)
    {
        foreach (var offset in Neighbors4Offsets)
        {
            var nx = x + offset.X;
            var ny = y + offset.Y;
            if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
                yield return new Vector2I(nx, ny);
        }
    }

    /// <summary>
    /// Enumerates the 4-directional neighbors of a position.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors(Vector2I pos) => GetNeighbors(pos.X, pos.Y);

    /// <summary>
    /// Enumerates all 8 neighbors of a position (N, NE, E, SE, S, SW, W, NW).
    /// Used for 2x2 window constraint propagation where diagonal cells share windows.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors8(int x, int y)
    {
        foreach (var offset in Neighbors8Offsets)
        {
            var nx = x + offset.X;
            var ny = y + offset.Y;
            if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
                yield return new Vector2I(nx, ny);
        }
    }

    /// <summary>
    /// Enumerates all 8 neighbors of a position.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors8(Vector2I pos) => GetNeighbors8(pos.X, pos.Y);

    /// <summary>
    /// Gets 4-directional neighbors into a pre-allocated span.
    /// Returns the number of valid neighbors written.
    /// Use this in hot paths to avoid iterator allocations.
    /// </summary>
    public int GetNeighborsNonAlloc(Vector2I pos, Span<Vector2I> output)
    {
        var count = 0;
        var x = pos.X;
        var y = pos.Y;

        foreach (var offset in Neighbors4Offsets)
        {
            var nx = x + offset.X;
            var ny = y + offset.Y;
            if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
            {
                if (count < output.Length)
                    output[count++] = new Vector2I(nx, ny);
            }
        }

        return count;
    }

    /// <summary>
    /// Gets 8-directional neighbors into a pre-allocated span.
    /// Returns the number of valid neighbors written.
    /// Use this in hot paths to avoid iterator allocations.
    /// </summary>
    public int GetNeighbors8NonAlloc(Vector2I pos, Span<Vector2I> output)
    {
        var count = 0;
        var x = pos.X;
        var y = pos.Y;

        foreach (var offset in Neighbors8Offsets)
        {
            var nx = x + offset.X;
            var ny = y + offset.Y;
            if (nx >= 0 && nx < _width && ny >= 0 && ny < _height)
            {
                if (count < output.Length)
                    output[count++] = new Vector2I(nx, ny);
            }
        }

        return count;
    }

    /// <summary>
    /// Checks if all cells have collapsed to a single tile.
    /// </summary>
    public bool IsFullyCollapsed()
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
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
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
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
    /// Finds uncollapsed cell with fewest remaining options, preferring frontier cells.
    /// Fast O(cells) scan - use this for cell selection, then apply weighted tile selection.
    /// </summary>
    public Vector2I? GetLowestOptionCountCell(RandomNumberGenerator rng)
    {
        var lowestCount = int.MaxValue;
        var candidates = new List<Vector2I>();
        var frontierCandidates = new List<Vector2I>();

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var cell = _cells[y, x];
                if (cell.IsCollapsed())
                    continue;

                var count = cell.GetPossibleTiles().Count;
                var pos = new Vector2I(x, y);

                if (count < lowestCount)
                {
                    lowestCount = count;
                    candidates.Clear();
                    frontierCandidates.Clear();
                    candidates.Add(pos);
                    if (HasCollapsedNeighbor(pos))
                        frontierCandidates.Add(pos);
                }
                else if (count == lowestCount)
                {
                    candidates.Add(pos);
                    if (HasCollapsedNeighbor(pos))
                        frontierCandidates.Add(pos);
                }
            }
        }

        if (candidates.Count == 0)
            return null;

        // Prefer frontier cells for contiguous region growth
        var selection = frontierCandidates.Count > 0 ? frontierCandidates : candidates;
        return selection[rng.RandiRange(0, selection.Count - 1)];
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
        RandomNumberGenerator rng)
    {
        // First pass: find frontier cells (adjacent to collapsed)
        var frontierCells = new List<Vector2I>();
        Vector2I? anyUncollapsed = null;

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var cell = _cells[y, x];
                if (cell.IsCollapsed()) continue;

                var pos = new Vector2I(x, y);
                anyUncollapsed ??= pos;

                if (HasCollapsedNeighbor(pos))
                    frontierCells.Add(pos);
            }
        }

        // If no frontier (start of generation), pick any uncollapsed cell randomly
        if (frontierCells.Count == 0)
            return anyUncollapsed;

        // Second pass: compute entropy ONLY for frontier cells
        var lowestEntropy = float.MaxValue;
        var candidates = new List<Vector2I>();

        foreach (var pos in frontierCells)
        {
            var cell = _cells[pos.Y, pos.X];
            var weights = getWeightsAt(pos);
            var entropy = cell.GetWeightedEntropy(weights);

            if (entropy < lowestEntropy)
            {
                lowestEntropy = entropy;
                candidates.Clear();
                candidates.Add(pos);
            }
            else if (Mathf.IsEqualApprox(entropy, lowestEntropy))
            {
                candidates.Add(pos);
            }
        }

        return candidates[rng.RandiRange(0, candidates.Count - 1)];
    }

    /// <summary>
    /// Enumerates all cell positions.
    /// </summary>
    public IEnumerable<Vector2I> GetAllPositions()
    {
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
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
    /// Creates a deep copy of this grid (for backtracking support).
    /// </summary>
    public WfcGrid Clone()
    {
        var clone = new WfcGrid(_width, _height, new List<string>());
        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                clone._cells[y, x] = new WfcCellState(_cells[y, x]);
            }
        }
        return clone;
    }
}
