using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Union-find over grid positions with path compression and union by rank.
/// Answers component queries in O(alpha(n)) amortized time.
/// </summary>
internal sealed class UnionFind
{
    private readonly Dictionary<Vector2I, Vector2I> _parent = new();
    private readonly Dictionary<Vector2I, int> _rank = new();

    /// <summary>
    /// Number of disjoint sets currently tracked.
    /// </summary>
    internal int ComponentCount { get; private set; }

    /// <summary>
    /// Adds a node as its own set. O(1).
    /// </summary>
    internal void AddNode(Vector2I position)
    {
        _parent[position] = position;
        _rank[position] = 0;
        ComponentCount++;
    }

    /// <summary>
    /// Checks if two nodes are in the same set.
    /// </summary>
    internal bool AreConnected(Vector2I a, Vector2I b) => Find(a) == Find(b);

    /// <summary>
    /// Merges the sets holding the two nodes. May reduce the component count.
    /// </summary>
    internal void Union(Vector2I a, Vector2I b)
    {
        var rootA = Find(a);
        var rootB = Find(b);

        if (rootA == rootB)
            return; // Already in same component

        // Union by rank
        if (_rank[rootA] < _rank[rootB])
        {
            _parent[rootA] = rootB;
        }
        else if (_rank[rootA] > _rank[rootB])
        {
            _parent[rootB] = rootA;
        }
        else
        {
            _parent[rootB] = rootA;
            _rank[rootA]++;
        }

        ComponentCount--;
    }

    /// <summary>
    /// Rebuilds the sets from an adjacency list.
    /// Needed after node removal, which can split components.
    /// </summary>
    internal void Rebuild(IEnumerable<Vector2I> nodes, Dictionary<Vector2I, HashSet<Vector2I>> adjacency)
    {
        _parent.Clear();
        _rank.Clear();
        ComponentCount = 0;

        // Re-initialize all nodes
        foreach (var node in nodes)
        {
            AddNode(node);
        }

        // Re-union based on edges
        foreach (var node in nodes)
        {
            foreach (var neighbor in adjacency[node])
            {
                Union(node, neighbor);
            }
        }
    }

    /// <summary>
    /// Find with path compression - O(alpha(n)) amortized.
    /// </summary>
    private Vector2I Find(Vector2I x)
    {
        if (!_parent.TryGetValue(x, out var p))
            return x;

        if (p != x)
        {
            _parent[x] = Find(p); // Path compression
        }
        return _parent[x];
    }
}
