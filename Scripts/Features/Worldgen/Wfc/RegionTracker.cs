using System;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Tracks contiguous regions of matching tile types during WFC collapse.
/// Uses union-find for O(1) amortized region merging and size queries.
/// </summary>
public class RegionTracker
{
    private int[] _parent;
    private int[] _size;
    private readonly int _width;
    private readonly int _height;

    public RegionTracker(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        _width = width;
        _height = height;
        var totalCells = width * height;
        _parent = new int[totalCells];
        _size = new int[totalCells];

        Reset();
    }

    /// <summary>
    /// Resets region tracking to initial state.
    /// Call before starting a new WFC generation.
    /// </summary>
    public void Reset()
    {
        for (var i = 0; i < _parent.Length; i++)
        {
            _parent[i] = i;
            _size[i] = 1;
        }
    }

    /// <summary>
    /// Notifies tracker that a tile has collapsed.
    /// Merges regions if matching neighbors exist.
    /// </summary>
    /// <param name="position">Position of collapsed tile</param>
    /// <param name="tileId">Collapsed tile ID</param>
    /// <param name="grid">WFC grid for neighbor queries</param>
    public void OnTileCollapsed(Vector2I position, string tileId, WfcGrid grid)
    {
        var currentIndex = ToIndex(position);

        foreach (var neighbor in grid.GetNeighbors(position))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighbor);
            if (neighborTile == tileId)
            {
                var neighborIndex = ToIndex(neighbor);
                Union(currentIndex, neighborIndex);
            }
        }
    }

    /// <summary>
    /// Gets the size of the region containing the given position.
    /// </summary>
    public int GetRegionSize(Vector2I position)
    {
        if (!IsValidPosition(position))
            return 1;

        var index = ToIndex(position);
        var root = Find(index);
        return _size[root];
    }

    private bool IsValidPosition(Vector2I pos)
    {
        return pos.X >= 0 && pos.X < _width && pos.Y >= 0 && pos.Y < _height;
    }

    private int ToIndex(Vector2I pos)
    {
        return pos.Y * _width + pos.X;
    }

    private int Find(int x)
    {
        if (_parent[x] != x)
        {
            _parent[x] = Find(_parent[x]); // Path compression
        }
        return _parent[x];
    }

    private void Union(int x, int y)
    {
        var rootX = Find(x);
        var rootY = Find(y);

        if (rootX == rootY)
            return;

        // Union by size for better tree balance
        if (_size[rootX] < _size[rootY])
        {
            _parent[rootX] = rootY;
            _size[rootY] += _size[rootX];
        }
        else
        {
            _parent[rootY] = rootX;
            _size[rootX] += _size[rootY];
        }
    }
}
