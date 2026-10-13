using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Modules per layer (#240 requirement 6): Data, Contracts and UI are one module each; the middle is clustered by
/// ModuleConsensus into "M&lt;id&gt;" modules and loose classes.
/// </summary>
internal static class LayeredModules
{
    public static ModuleAssignment Assign(ClassGraph graph)
    {
        var layers = LayerAnchors.Classify(graph);
        var module = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var loose = new SortedSet<string>(StringComparer.Ordinal);
        var moduleLayer = new SortedDictionary<string, Layer>(StringComparer.Ordinal);
        foreach (var (id, layer) in layers.Where(p => p.Value != Layer.Middle))
        {
            module[id] = layer.ToString();
            moduleLayer[layer.ToString()] = layer;
        }

        var middle = layers.Where(p => p.Value == Layer.Middle).Select(p => p.Key).ToList();
        var consensus = ModuleConsensus.Cluster(middle, graph.Nodes.ToDictionary(p => p.Key, p => p.Value.Home),
            MiddleEdges(graph, middle));
        var clustered = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (id, number) in consensus.Module)
        {
            var key = "M" + number;
            module[id] = key;
            moduleLayer[key] = Layer.Middle;
            clustered.Add(key);
        }

        loose.UnionWith(consensus.Loose);
        foreach (var id in loose) moduleLayer[ModuleAssignment.LooseKey(id)] = Layer.Middle;
        return new ModuleAssignment(layers, module, loose, moduleLayer, Names(graph, module, moduleLayer, clustered));
    }

    private static List<(string From, string To, double Cw)> MiddleEdges(ClassGraph graph, List<string> middle)
    {
        var set = middle.ToHashSet(StringComparer.Ordinal);
        return graph.Edges.Where(e => set.Contains(e.Key.From) && set.Contains(e.Key.To))
            .Select(e => (e.Key.From, e.Key.To, e.Value.ClusterWeight)).ToList();
    }

    /// <summary>
    /// Anchors by layer name; a middle module by its id and its class with the highest intra-module degree.
    /// </summary>
    private static SortedDictionary<string, string> Names(ClassGraph graph, SortedDictionary<string, string> module,
        SortedDictionary<string, Layer> moduleLayer, SortedSet<string> clustered)
    {
        var degree = new SortedDictionary<string, double>(StringComparer.Ordinal);
        foreach (var (key, weight) in graph.Edges)
        {
            if (!module.TryGetValue(key.From, out var a) || !module.TryGetValue(key.To, out var b) || a != b) continue;
            degree[key.From] = degree.GetValueOrDefault(key.From) + weight.ClusterWeight;
            degree[key.To] = degree.GetValueOrDefault(key.To) + weight.ClusterWeight;
        }

        var names = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var key in moduleLayer.Keys) names[key] = key;
        foreach (var group in module.GroupBy(p => p.Value).Where(g => clustered.Contains(g.Key)))
        {
            var top = group.Select(p => p.Key).OrderByDescending(id => degree.GetValueOrDefault(id))
                .ThenBy(id => id, StringComparer.Ordinal).First();
            names[group.Key] = group.Key + " " + top;
        }

        return names;
    }
}
