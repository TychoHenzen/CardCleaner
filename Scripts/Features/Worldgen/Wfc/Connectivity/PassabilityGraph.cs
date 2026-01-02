using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Graph representation of passable tiles for connectivity analysis.
/// Supports incremental updates and articulation point detection using Tarjan's algorithm.
/// </summary>
public class PassabilityGraph
{
    private readonly HashSet<Vector2I> _nodes = new();
    private readonly Dictionary<Vector2I, HashSet<Vector2I>> _adjacency = new();

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
    /// Special case: In a 2-node graph A--B, both nodes are considered articulation points
    /// for WFC purposes (removing either isolates the other from future expansion).
    /// </summary>
    private HashSet<Vector2I> FindAllArticulationPoints()
    {
        var articulationPoints = new HashSet<Vector2I>();

        if (_nodes.Count <= 1)
            return articulationPoints;

        // Special case: 2-node graph
        // For WFC connectivity, both nodes in a connected pair are critical
        // (removing either prevents the other from connecting to future tiles)
        if (_nodes.Count == 2)
        {
            // Check if the two nodes are connected
            var nodesList = _nodes.ToList();
            if (_adjacency[nodesList[0]].Contains(nodesList[1]))
            {
                // They're connected, both are critical
                articulationPoints.Add(nodesList[0]);
                articulationPoints.Add(nodesList[1]);
            }
            return articulationPoints;
        }

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
