using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;

/// <summary>
/// Tracks connected blob sizes during WFC generation.
/// Uses array-based union-find (disjoint set) for efficient blob size queries.
/// </summary>
/// <remarks>
/// Optimized version using flat arrays instead of Dictionary&lt;Vector2I, _&gt;.
/// Array indexing is O(1) vs Dictionary hash lookup, which matters in hot paths.
/// </remarks>
public class BlobSizeTracker
{
    private int[]? _parent;
    private int[]? _size;
    private string?[]? _tileType;
    private int _width;
    private int _height;
    private bool _initialized;

    /// <summary>
    /// Initializes the tracker for a grid of the given size.
    /// Must be called before RegisterCollapse.
    /// </summary>
    public void Initialize(int width, int height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height));

        _width = width;
        _height = height;
        var totalCells = width * height;

        _parent = new int[totalCells];
        _size = new int[totalCells];
        _tileType = new string?[totalCells];

        Reset();
        _initialized = true;
    }

    /// <summary>
    /// Resets tracking state without reallocating arrays.
    /// Call this when starting a new generation with the same grid size.
    /// </summary>
    public void Reset()
    {
        if (_parent == null || _size == null || _tileType == null)
            return;

        for (var i = 0; i < _parent.Length; i++)
        {
            _parent[i] = i;
            _size[i] = 1;
            _tileType[i] = null;
        }
    }

    /// <summary>
    /// Registers a collapsed tile at the given position.
    /// Merges with adjacent tiles of the same type.
    /// </summary>
    public void RegisterCollapse(Vector2I position, string tileId, WfcGrid grid)
    {
        // Auto-initialize if needed (for backward compatibility)
        if (!_initialized)
        {
            Initialize(grid.Width, grid.Height);
        }

        var index = ToIndex(position);
        _tileType![index] = tileId;
        // parent and size already initialized to self/1 in Reset()

        // Merge with adjacent collapsed tiles of same type (4-directional)
        foreach (var neighborPos in grid.GetNeighbors(position))
        {
            var neighborIndex = ToIndex(neighborPos);
            if (_tileType[neighborIndex] == tileId)
            {
                Union(index, neighborIndex);
            }
        }
    }

    /// <summary>
    /// Gets the size of the connected blob containing the given position.
    /// Returns 0 if position hasn't been registered.
    /// </summary>
    public int GetBlobSize(Vector2I position)
    {
        if (!_initialized)
            return 0;

        var index = ToIndex(position);
        if (_tileType![index] == null)
            return 0;

        var root = Find(index);
        return _size![root];
    }

    /// <summary>
    /// Gets the size of the blob that would exist if the given tile were placed
    /// at the specified position (hypothetical query without modifying state).
    /// </summary>
    public int GetPotentialBlobSize(Vector2I position, string tileId, WfcGrid grid)
    {
        if (!_initialized)
            return 1;

        var size = 1; // Start with this tile itself
        var counted = new HashSet<int>(); // Track counted roots by index

        foreach (var neighborPos in grid.GetNeighbors(position))
        {
            var neighborIndex = ToIndex(neighborPos);
            if (_tileType![neighborIndex] == tileId)
            {
                var root = Find(neighborIndex);
                if (counted.Add(root))
                {
                    size += _size![root];
                }
            }
        }

        return size;
    }

    /// <summary>
    /// Clears all tracked data.
    /// </summary>
    public void Clear()
    {
        Reset();
    }

    private int ToIndex(Vector2I pos)
    {
        return pos.Y * _width + pos.X;
    }

    private int Find(int x)
    {
        if (_parent![x] != x)
        {
            _parent[x] = Find(_parent[x]); // Path compression
        }
        return _parent[x];
    }

    private void Union(int a, int b)
    {
        var rootA = Find(a);
        var rootB = Find(b);

        if (rootA == rootB)
            return;

        // Union by size for better tree balance
        if (_size![rootA] < _size[rootB])
        {
            _parent![rootA] = rootB;
            _size[rootB] += _size[rootA];
        }
        else
        {
            _parent![rootB] = rootA;
            _size[rootA] += _size[rootB];
        }
    }
}
