using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;

/// <summary>
/// Tracks connected blob sizes during WFC generation.
/// Uses union-find (disjoint set) for efficient blob size queries.
/// </summary>
public class BlobSizeTracker
{
    private readonly Dictionary<Vector2I, Vector2I> _parent = new();
    private readonly Dictionary<Vector2I, int> _rank = new();
    private readonly Dictionary<Vector2I, int> _size = new();
    private readonly Dictionary<Vector2I, string> _tileType = new();

    /// <summary>
    /// Registers a collapsed tile at the given position.
    /// Merges with adjacent tiles of the same type.
    /// </summary>
    public void RegisterCollapse(Vector2I position, string tileId, WfcGrid grid)
    {
        // Initialize this position as its own set
        _parent[position] = position;
        _rank[position] = 0;
        _size[position] = 1;
        _tileType[position] = tileId;

        // Merge with adjacent collapsed tiles of same type (4-directional)
        foreach (var neighborPos in grid.GetNeighbors(position))
        {
            if (_tileType.TryGetValue(neighborPos, out var neighborType) && neighborType == tileId)
            {
                Union(position, neighborPos);
            }
        }
    }

    /// <summary>
    /// Gets the size of the connected blob containing the given position.
    /// Returns 0 if position hasn't been registered.
    /// </summary>
    public int GetBlobSize(Vector2I position)
    {
        if (!_parent.ContainsKey(position))
            return 0;

        var root = Find(position);
        return _size[root];
    }

    /// <summary>
    /// Gets the size of the blob that would exist if the given tile were placed
    /// at the specified position (hypothetical query without modifying state).
    /// </summary>
    public int GetPotentialBlobSize(Vector2I position, string tileId, WfcGrid grid)
    {
        var size = 1; // Start with this tile itself
        var counted = new HashSet<Vector2I>();

        foreach (var neighborPos in grid.GetNeighbors(position))
        {
            if (_tileType.TryGetValue(neighborPos, out var neighborType) && neighborType == tileId)
            {
                var root = Find(neighborPos);
                if (counted.Add(root))
                {
                    size += _size[root];
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
        _parent.Clear();
        _rank.Clear();
        _size.Clear();
        _tileType.Clear();
    }

    private Vector2I Find(Vector2I pos)
    {
        if (_parent[pos] != pos)
        {
            _parent[pos] = Find(_parent[pos]); // Path compression
        }
        return _parent[pos];
    }

    private void Union(Vector2I a, Vector2I b)
    {
        var rootA = Find(a);
        var rootB = Find(b);

        if (rootA == rootB)
            return;

        // Union by rank
        if (_rank[rootA] < _rank[rootB])
        {
            _parent[rootA] = rootB;
            _size[rootB] += _size[rootA];
        }
        else if (_rank[rootA] > _rank[rootB])
        {
            _parent[rootB] = rootA;
            _size[rootA] += _size[rootB];
        }
        else
        {
            _parent[rootB] = rootA;
            _size[rootA] += _size[rootB];
            _rank[rootA]++;
        }
    }
}
