using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Graph representation of passable tiles for connectivity analysis.
/// Uses union-find for O(alpha(n)) amortized component queries instead of O(V) BFS.
/// Supports incremental updates and disconnected component tracking.
/// </summary>
public class PassabilityGraph
{
    private readonly HashSet<Vector2I> _nodes = new();
    private readonly Dictionary<Vector2I, HashSet<Vector2I>> _adjacency = new();

    // Union-find for O(1) amortized component queries
    private readonly UnionFind _unionFind = new();

    // Cached data - only invalidated when truly needed
    private List<HashSet<Vector2I>>? _cachedComponents;
    private ClosestPair? _cachedClosestPair;
    private List<ClosestPair>? _cachedAllClosestPairs;

    /// <summary>
    /// Number of nodes in the graph.
    /// </summary>
    public int NodeCount => _nodes.Count;

    /// <summary>
    /// Checks if the graph contains a node at the given position.
    /// </summary>
    public bool ContainsNode(Vector2I position) => _nodes.Contains(position);

    /// <summary>
    /// Gets the neighbors of a node. Returns empty enumerable if node doesn't exist.
    /// </summary>
    public IEnumerable<Vector2I> GetNeighbors(Vector2I position)
    {
        return _adjacency.TryGetValue(position, out var neighbors) ? neighbors : Enumerable.Empty<Vector2I>();
    }

    /// <summary>
    /// Adds a node to the graph at the given position.
    /// O(1) operation using union-find.
    /// </summary>
    public void AddNode(Vector2I position)
    {
        if (!_nodes.Add(position))
            return;

        _adjacency[position] = new HashSet<Vector2I>();
        _unionFind.AddNode(position);

        // A new node is a new component, so every cached component and closest-pair result is stale.
        InvalidateCache();
    }

    /// <summary>
    /// Removes a node from the graph and all edges connected to it.
    /// Note: This invalidates union-find and requires rebuild.
    /// </summary>
    public void RemoveNode(Vector2I position)
    {
        if (!_nodes.Remove(position)) return;

        // Remove all edges to this node from neighbors
        if (_adjacency.TryGetValue(position, out var neighbors))
        {
            foreach (var neighbor in neighbors)
            {
                _adjacency[neighbor].Remove(position);
            }
        }
        _adjacency.Remove(position);

        // Node removal can split components - need full rebuild
        _unionFind.Rebuild(_nodes, _adjacency);
        InvalidateCache();
    }

    /// <summary>
    /// Removes every node and edge and drops all cached results.
    /// </summary>
    internal void Clear()
    {
        _nodes.Clear();
        _adjacency.Clear();
        _unionFind.Rebuild(_nodes, _adjacency);
        InvalidateCache();
    }

    /// <summary>
    /// Adds a bidirectional edge between two nodes.
    /// Implicitly adds nodes if they don't exist.
    /// O(alpha(n)) amortized using union-find.
    /// </summary>
    public void AddEdge(Vector2I a, Vector2I b)
    {
        AddNode(a);
        AddNode(b);

        // Only add edge if not already present
        if (_adjacency[a].Add(b))
        {
            _adjacency[b].Add(a);

            // Union the components - this may reduce component count
            _unionFind.Union(a, b);

            InvalidateCache();
        }
    }

    /// <summary>
    /// Checks if two nodes are in the same component.
    /// O(alpha(n)) amortized.
    /// </summary>
    public bool AreConnected(Vector2I a, Vector2I b)
    {
        if (!_nodes.Contains(a) || !_nodes.Contains(b))
            return false;
        return _unionFind.AreConnected(a, b);
    }

    private void InvalidateCache()
    {
        _cachedComponents = null;
        _cachedClosestPair = null;
        _cachedAllClosestPairs = null;
    }

    /// <summary>
    /// Gets all connected components in the graph.
    /// Results are cached until the graph structure changes.
    /// </summary>
    public List<HashSet<Vector2I>> GetComponents()
    {
        if (_cachedComponents != null)
            return _cachedComponents;

        _cachedComponents = new List<HashSet<Vector2I>>();
        var visited = new HashSet<Vector2I>();

        foreach (var node in _nodes)
        {
            if (visited.Contains(node))
                continue;

            var component = new HashSet<Vector2I>();
            var queue = new Queue<Vector2I>();
            queue.Enqueue(node);
            visited.Add(node);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                foreach (var neighbor in _adjacency[current])
                {
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            _cachedComponents.Add(component);
        }

        return _cachedComponents;
    }

    /// <summary>
    /// Returns true if the graph has multiple disconnected components.
    /// O(1) using union-find component count.
    /// </summary>
    public bool HasDisconnectedRegions()
    {
        return _unionFind.ComponentCount > 1;
    }

    /// <summary>
    /// Gets the two closest nodes from different components.
    /// Returns null if graph has fewer than 2 components.
    /// Results are cached until the graph structure changes.
    /// </summary>
    public (Vector2I, Vector2I)? GetClosestDisconnectedPair()
    {
        if (!_cachedClosestPair.HasValue)
        {
            var components = GetComponents();
            if (components.Count < 2)
                return null;

            _cachedClosestPair = ComponentPairFinder.FindOverallClosest(components);
        }

        var pair = _cachedClosestPair.Value;
        return (pair.First, pair.Second);
    }

    /// <summary>
    /// Gets all closest pairs between disconnected components.
    /// Results are cached until the graph structure changes.
    /// </summary>
    private List<ClosestPair> GetAllClosestPairs()
    {
        if (_cachedAllClosestPairs != null)
            return _cachedAllClosestPairs;

        var components = GetComponents();
        _cachedAllClosestPairs = components.Count < 2
            ? new List<ClosestPair>()
            : ComponentPairFinder.FindClosestPerComponentPair(components);

        return _cachedAllClosestPairs;
    }

    /// <summary>
    /// Checks if a position is on or near the Manhattan path between ANY pair of disconnected nodes.
    /// Used to identify "corridor" positions that should prefer passable tiles.
    /// This ensures ALL disconnected components eventually connect, not just the closest pair.
    /// </summary>
    /// <param name="position">Position to check</param>
    /// <param name="tolerance">Maximum perpendicular distance from the path (default 1 = adjacent to path)</param>
    public bool IsOnCorridorPath(Vector2I position, int tolerance = 1)
    {
        foreach (var pair in GetAllClosestPairs())
        {
            if (ManhattanCorridor.IsOnPath(position, pair.First, pair.Second, tolerance))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if removing this node would disconnect the graph.
    /// Uses Tarjan's algorithm for articulation point detection.
    /// </summary>
    /// <param name="position">The position to check.</param>
    /// <returns>True if removing this node would disconnect the graph.</returns>
    public bool IsArticulationPoint(Vector2I position)
    {
        // Empty graph or node not in graph
        if (_nodes.Count == 0 || !_nodes.Contains(position))
            return false;

        // Single node cannot be an articulation point
        if (_nodes.Count == 1)
            return false;

        // Use Tarjan's algorithm to find all articulation points
        var articulationPoints = ArticulationPointFinder.FindAll(_nodes, _adjacency);
        return articulationPoints.Contains(position);
    }
}
