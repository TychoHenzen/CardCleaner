using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Finds all articulation points of a graph using Tarjan's algorithm.
/// An articulation point is a vertex whose removal disconnects the graph.
/// </summary>
internal sealed class ArticulationPointFinder
{
    private readonly Dictionary<Vector2I, HashSet<Vector2I>> _adjacency;
    private readonly HashSet<Vector2I> _articulationPoints = new();
    private readonly Dictionary<Vector2I, int> _discoveryTime = new();
    private readonly Dictionary<Vector2I, int> _lowLink = new();
    private readonly Dictionary<Vector2I, Vector2I?> _parent = new();
    private int _time;

    private ArticulationPointFinder(Dictionary<Vector2I, HashSet<Vector2I>> adjacency)
    {
        _adjacency = adjacency;
    }

    internal static HashSet<Vector2I> FindAll(
        IEnumerable<Vector2I> nodes,
        Dictionary<Vector2I, HashSet<Vector2I>> adjacency)
    {
        var finder = new ArticulationPointFinder(adjacency);

        // Run DFS from all unvisited nodes to handle disconnected graphs
        foreach (var node in nodes)
        {
            if (finder._discoveryTime.ContainsKey(node))
                continue;

            finder._parent[node] = null;
            finder.Visit(node);
        }

        return finder._articulationPoints;
    }

    private void Visit(Vector2I node)
    {
        _discoveryTime[node] = _lowLink[node] = _time++;
        var childCount = 0;

        foreach (var neighbor in _adjacency[node])
        {
            if (!_discoveryTime.TryGetValue(neighbor, out var neighborDiscovery))
            {
                // Neighbor not yet visited
                childCount++;
                _parent[neighbor] = node;
                Visit(neighbor);

                // Update low-link value
                _lowLink[node] = Math.Min(_lowLink[node], _lowLink[neighbor]);

                if (IsCutVertexThrough(node, neighbor, childCount))
                    _articulationPoints.Add(node);
            }
            else if (_parent.TryGetValue(node, out var p) && neighbor != p)
            {
                // Back edge (not to parent)
                _lowLink[node] = Math.Min(_lowLink[node], neighborDiscovery);
            }
        }
    }

    /// <summary>
    /// Tests the articulation conditions after finishing a DFS child.
    /// 1. Root of the DFS tree with 2+ children.
    /// 2. Non-root where no descendant has a back-edge to an ancestor of the node.
    /// </summary>
    private bool IsCutVertexThrough(Vector2I node, Vector2I child, int childCount)
    {
        if (!_parent.TryGetValue(node, out var nodeParent) || nodeParent == null)
            return childCount >= 2;

        return _lowLink[child] >= _discoveryTime[node];
    }
}
