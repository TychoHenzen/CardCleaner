using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// 2D grid of WFC cells. Manages cell access and neighbor enumeration.
/// </summary>
public class WfcGrid
{
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
        // North
        if (y > 0) yield return new Vector2I(x, y - 1);
        // East
        if (x < _width - 1) yield return new Vector2I(x + 1, y);
        // South
        if (y < _height - 1) yield return new Vector2I(x, y + 1);
        // West
        if (x > 0) yield return new Vector2I(x - 1, y);
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
        // North
        if (y > 0) yield return new Vector2I(x, y - 1);
        // North-East
        if (y > 0 && x < _width - 1) yield return new Vector2I(x + 1, y - 1);
        // East
        if (x < _width - 1) yield return new Vector2I(x + 1, y);
        // South-East
        if (y < _height - 1 && x < _width - 1) yield return new Vector2I(x + 1, y + 1);
        // South
        if (y < _height - 1) yield return new Vector2I(x, y + 1);
        // South-West
        if (y < _height - 1 && x > 0) yield return new Vector2I(x - 1, y + 1);
        // West
        if (x > 0) yield return new Vector2I(x - 1, y);
        // North-West
        if (y > 0 && x > 0) yield return new Vector2I(x - 1, y - 1);
    }

    /// <summary>
    /// Enumerates all 8 neighbors of a position.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors8(Vector2I pos) => GetNeighbors8(pos.X, pos.Y);

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
    /// Finds the uncollapsed cell with the lowest entropy.
    /// Returns null if all cells are collapsed.
    /// When multiple cells have the same entropy, returns the first one found
    /// (for deterministic behavior, use GetLowestEntropyCellWithTieBreak).
    /// </summary>
    public Vector2I? GetLowestEntropyCell()
    {
        Vector2I? best = null;
        var lowestEntropy = int.MaxValue;

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var cell = _cells[y, x];
                if (cell.IsCollapsed())
                    continue;

                var entropy = cell.GetEntropy();
                if (entropy < lowestEntropy)
                {
                    lowestEntropy = entropy;
                    best = new Vector2I(x, y);
                }
            }
        }

        return best;
    }

    /// <summary>
    /// Finds uncollapsed cells with the lowest entropy, then randomly selects one.
    /// This adds randomness to the collapse order for more varied results.
    /// </summary>
    public Vector2I? GetLowestEntropyCellWithTieBreak(RandomNumberGenerator rng)
    {
        var lowestEntropy = int.MaxValue;
        var candidates = new List<Vector2I>();

        for (var y = 0; y < _height; y++)
        {
            for (var x = 0; x < _width; x++)
            {
                var cell = _cells[y, x];
                if (cell.IsCollapsed())
                    continue;

                var entropy = cell.GetEntropy();
                if (entropy < lowestEntropy)
                {
                    lowestEntropy = entropy;
                    candidates.Clear();
                    candidates.Add(new Vector2I(x, y));
                }
                else if (entropy == lowestEntropy)
                {
                    candidates.Add(new Vector2I(x, y));
                }
            }
        }

        if (candidates.Count == 0)
            return null;

        var index = rng.RandiRange(0, candidates.Count - 1);
        return candidates[index];
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
