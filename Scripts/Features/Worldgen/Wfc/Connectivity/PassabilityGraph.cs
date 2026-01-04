using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Graph representation of passable tiles for connectivity analysis.
/// Supports incremental updates, articulation point detection, and disconnected component tracking.
/// </summary>
public class PassabilityGraph
{
    private readonly HashSet<Vector2I> _nodes = new();
    private readonly Dictionary<Vector2I, HashSet<Vector2I>> _adjacency = new();

    // Cached component data - invalidated on structural changes
    private List<HashSet<Vector2I>>? _cachedComponents;
    private (Vector2I, Vector2I)? _cachedClosestPair;

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
    /// </summary>
    public void AddNode(Vector2I position)
    {
        if (_nodes.Add(position))
        {
            _adjacency[position] = new HashSet<Vector2I>();
            InvalidateCache();
        }
    }

    /// <summary>
    /// Removes a node from the graph and all edges connected to it.
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
        InvalidateCache();
    }

    /// <summary>
    /// Adds a bidirectional edge between two nodes.
    /// Implicitly adds nodes if they don't exist.
    /// </summary>
    public void AddEdge(Vector2I a, Vector2I b)
    {
        AddNode(a);
        AddNode(b);
        _adjacency[a].Add(b);
        _adjacency[b].Add(a);
        InvalidateCache();
    }

    private void InvalidateCache()
    {
        _cachedComponents = null;
        _cachedClosestPair = null;
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
    /// </summary>
    public bool HasDisconnectedRegions()
    {
        var components = GetComponents();
        return components.Count > 1;
    }

    /// <summary>
    /// Gets the two closest nodes from different components.
    /// Returns null if graph has fewer than 2 components.
    /// Results are cached until the graph structure changes.
    /// </summary>
    public (Vector2I, Vector2I)? GetClosestDisconnectedPair()
    {
        if (_cachedClosestPair.HasValue)
            return _cachedClosestPair;

        var components = GetComponents();
        if (components.Count < 2)
            return null;

        // Find the two closest nodes across all component pairs
        var minDistance = float.MaxValue;
        Vector2I closest1 = default, closest2 = default;

        for (var i = 0; i < components.Count - 1; i++)
        {
            for (var j = i + 1; j < components.Count; j++)
            {
                foreach (var node1 in components[i])
                {
                    foreach (var node2 in components[j])
                    {
                        var dist = ManhattanDistance(node1, node2);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closest1 = node1;
                            closest2 = node2;
                        }
                    }
                }
            }
        }

        _cachedClosestPair = (closest1, closest2);
        return _cachedClosestPair;
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
        var components = GetComponents();
        if (components.Count < 2)
            return false;

        // Check all pairs of components for corridor paths
        for (var i = 0; i < components.Count - 1; i++)
        {
            for (var j = i + 1; j < components.Count; j++)
            {
                // Find closest nodes between this pair of components
                var minDistance = float.MaxValue;
                Vector2I closest1 = default, closest2 = default;

                foreach (var node1 in components[i])
                {
                    foreach (var node2 in components[j])
                    {
                        var dist = ManhattanDistance(node1, node2);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            closest1 = node1;
                            closest2 = node2;
                        }
                    }
                }

                // Check if position is on the corridor path between this pair
                if (IsOnManhattanPath(position, closest1, closest2, tolerance))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a position is on or near the L-shaped Manhattan path between two points.
    /// The path goes horizontal first, then vertical (or the reverse).
    /// </summary>
    private static bool IsOnManhattanPath(Vector2I pos, Vector2I from, Vector2I to, int tolerance)
    {
        // Path 1: horizontal then vertical
        // From (from.X, from.Y) to (to.X, from.Y) to (to.X, to.Y)
        if (IsNearLineSegment(pos, from, new Vector2I(to.X, from.Y), tolerance) ||
            IsNearLineSegment(pos, new Vector2I(to.X, from.Y), to, tolerance))
            return true;

        // Path 2: vertical then horizontal
        // From (from.X, from.Y) to (from.X, to.Y) to (to.X, to.Y)
        if (IsNearLineSegment(pos, from, new Vector2I(from.X, to.Y), tolerance) ||
            IsNearLineSegment(pos, new Vector2I(from.X, to.Y), to, tolerance))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a position is within tolerance of an axis-aligned line segment.
    /// </summary>
    private static bool IsNearLineSegment(Vector2I pos, Vector2I a, Vector2I b, int tolerance)
    {
        // Horizontal segment
        if (a.Y == b.Y)
        {
            var minX = Math.Min(a.X, b.X);
            var maxX = Math.Max(a.X, b.X);
            return pos.X >= minX - tolerance && pos.X <= maxX + tolerance &&
                   Math.Abs(pos.Y - a.Y) <= tolerance;
        }

        // Vertical segment
        if (a.X == b.X)
        {
            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            return pos.Y >= minY - tolerance && pos.Y <= maxY + tolerance &&
                   Math.Abs(pos.X - a.X) <= tolerance;
        }

        return false;
    }

    private static int ManhattanDistance(Vector2I a, Vector2I b)
    {
        return Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
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
        var articulationPoints = FindAllArticulationPoints();
        return articulationPoints.Contains(position);
    }

    /// <summary>
    /// Finds all articulation points in the graph using Tarjan's algorithm.
    /// An articulation point is a vertex whose removal disconnects the graph.
    /// </summary>
    private HashSet<Vector2I> FindAllArticulationPoints()
    {
        var articulationPoints = new HashSet<Vector2I>();

        if (_nodes.Count <= 1)
            return articulationPoints;

        var discoveryTime = new Dictionary<Vector2I, int>();
        var lowLink = new Dictionary<Vector2I, int>();
        var parent = new Dictionary<Vector2I, Vector2I?>();
        var time = 0;

        void Dfs(Vector2I node)
        {
            discoveryTime[node] = lowLink[node] = time++;
            var childCount = 0;

            foreach (var neighbor in _adjacency[node])
            {
                if (!discoveryTime.ContainsKey(neighbor))
                {
                    // Neighbor not yet visited
                    childCount++;
                    parent[neighbor] = node;
                    Dfs(neighbor);

                    // Update low-link value
                    lowLink[node] = Math.Min(lowLink[node], lowLink[neighbor]);

                    // Check articulation point conditions:
                    // 1. Root of DFS tree with 2+ children
                    if (!parent.TryGetValue(node, out var nodeParent) || nodeParent == null)
                    {
                        if (childCount >= 2)
                        {
                            articulationPoints.Add(node);
                        }
                    }
                    // 2. Non-root where no descendant has back-edge to ancestor of node
                    else if (lowLink[neighbor] >= discoveryTime[node])
                    {
                        articulationPoints.Add(node);
                    }
                }
                else if (parent.TryGetValue(node, out var p) && neighbor != p)
                {
                    // Back edge (not to parent)
                    lowLink[node] = Math.Min(lowLink[node], discoveryTime[neighbor]);
                }
            }
        }

        // Run DFS from all unvisited nodes to handle disconnected graphs
        foreach (var node in _nodes)
        {
            if (!discoveryTime.ContainsKey(node))
            {
                parent[node] = null;
                Dfs(node);
            }
        }

        return articulationPoints;
    }
}
