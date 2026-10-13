using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The three anchor layers (#240 requirements 4, 5 and 5a): UI on top, pure data at the bottom and pure contracts
/// above it. Every other class is in the middle layer.
/// </summary>
internal static class LayerAnchors
{
    public static SortedDictionary<string, Layer> Classify(ClassGraph graph)
    {
        var ui = UiClasses(graph);
        var data = PureData(graph, ui);
        var contracts = PureContracts(graph, ui, data);
        var layers = new SortedDictionary<string, Layer>(StringComparer.Ordinal);
        foreach (var id in graph.Nodes.Keys)
        {
            layers[id] = ui.Contains(id) ? Layer.UI
                : data.Contains(id) ? Layer.Data
                : contracts.Contains(id) ? Layer.Contracts
                : Layer.Middle;
        }

        return layers;
    }

    /// <summary>
    /// UI seeds, then interfaces, delegates and abstract types that name a UI class and have at least one user, all of
    /// them UI.
    /// </summary>
    public static SortedSet<string> UiClasses(ClassGraph graph)
    {
        var refs = References(graph);
        var users = Users(graph);
        var ui = new SortedSet<string>(graph.Nodes.Values.Where(n => n.UiSeed).Select(n => n.Id),
            StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var node in graph.Nodes.Values)
            {
                if (ui.Contains(node.Id) || node.Role is not (NodeRole.Interface or NodeRole.Abstract)) continue;
                var nodeUsers = users[node.Id];
                if (nodeUsers.Count == 0 || !refs[node.Id].Any(ui.Contains) || !nodeUsers.All(ui.Contains)) continue;
                ui.Add(node.Id);
                changed = true;
            }
        }

        return ui;
    }

    /// <summary>The fixed point of: role data, not UI, and every referenced class is pure data.</summary>
    public static SortedSet<string> PureData(ClassGraph graph, ISet<string> ui)
    {
        var start = graph.Nodes.Values.Where(n => n.Role == NodeRole.Data && !ui.Contains(n.Id)).Select(n => n.Id);
        return FixedPoint(References(graph), start, _ => false);
    }

    /// <summary>
    /// The fixed point of: role interface, not UI, and every referenced class is pure data or in the set.
    /// </summary>
    public static SortedSet<string> PureContracts(ClassGraph graph, ISet<string> ui, ISet<string> data)
    {
        var start = graph.Nodes.Values.Where(n => n.Role == NodeRole.Interface && !ui.Contains(n.Id))
            .Select(n => n.Id);
        return FixedPoint(References(graph), start, data.Contains);
    }

    private static SortedSet<string> FixedPoint(SortedDictionary<string, SortedSet<string>> refs,
        IEnumerable<string> start, Func<string, bool> alsoAllowed)
    {
        var set = new SortedSet<string>(start, StringComparer.Ordinal);
        var changed = true;
        while (changed)
        {
            var drop = set.Where(id => !refs[id].All(r => set.Contains(r) || alsoAllowed(r))).ToList();
            set.ExceptWith(drop);
            changed = drop.Count > 0;
        }

        return set;
    }

    internal static SortedDictionary<string, SortedSet<string>> References(ClassGraph graph)
    {
        var refs = Empty(graph);
        foreach (var key in graph.Edges.Keys) refs[key.From].Add(key.To);
        return refs;
    }

    internal static SortedDictionary<string, SortedSet<string>> Users(ClassGraph graph)
    {
        var users = Empty(graph);
        foreach (var key in graph.Edges.Keys) users[key.To].Add(key.From);
        return users;
    }

    private static SortedDictionary<string, SortedSet<string>> Empty(ClassGraph graph)
    {
        var map = new SortedDictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        foreach (var id in graph.Nodes.Keys) map[id] = new SortedSet<string>(StringComparer.Ordinal);
        return map;
    }
}
