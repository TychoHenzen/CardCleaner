using System;
using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>The class graph of Scripts/: nodes and weighted directed edges, both iterated in ordinal order.</summary>
internal sealed class ClassGraph
{
    public SortedDictionary<string, GateNode> Nodes { get; } = new(StringComparer.Ordinal);

    public SortedDictionary<EdgeKey, EdgeWeight> Edges { get; } = new();

    public static int KindWeight(EdgeKind kind) => kind switch
    {
        EdgeKind.Inherits => 3,
        EdgeKind.MemberType => 2,
        EdgeKind.Creates => 2,
        _ => 1,
    };

    public void AddNode(GateNode node) => Nodes.Add(node.Id, node);

    public void AddNode(string id, NodeRole role, bool uiSeed = false) =>
        AddNode(new GateNode(id, role, "Scripts", uiSeed));

    /// <summary>
    /// Adds count reference sites of one kind; sites of one pair and kind merge across calls. Self references,
    /// unknown classes and non-positive counts are ignored.
    /// </summary>
    public void AddEdge(string from, string to, EdgeKind kind, int count = 1)
    {
        if (count <= 0 || from == to || !Nodes.ContainsKey(from) || !Nodes.ContainsKey(to)) return;
        var key = new EdgeKey(from, to);
        if (!Edges.TryGetValue(key, out var weight))
        {
            weight = new EdgeWeight();
            Edges.Add(key, weight);
        }

        weight.Add(kind, count);
    }
}
