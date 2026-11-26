using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Looks inside the UI layer (#484). The older gated numbers see every UI class as one module, so a loop between the
/// hand, the shelves and the checkout cannot show there. This clusters only the UI classes, over only the references
/// between them, with the same consensus as the middle layer (ModuleConsensus), and orders the clusters on their own:
/// their up edges and loops are the two UI numbers. References between UI modules are never rated as seams: node
/// scripts reach each other through exported node references, and an interface for each would only satisfy the gate.
/// </summary>
internal static class UiModules
{
    private const string KeyPrefix = "U";

    public static UiView Measure(ClassGraph graph, IReadOnlyDictionary<string, Layer> layers)
    {
        var modules = Assign(graph, layers);
        var edges = ModuleEdges.Build(graph, modules.SpreadModule);
        return new UiView(modules, edges, ModuleOrder.Order(modules.ModuleLayer, edges));
    }

    private static UiModuleAssignment Assign(ClassGraph graph, IReadOnlyDictionary<string, Layer> layers)
    {
        var ui = layers.Where(p => p.Value == Layer.UI).Select(p => p.Key).ToList();
        var uiSet = ui.ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges.Where(e => uiSet.Contains(e.Key.From) && uiSet.Contains(e.Key.To))
            .Select(e => (e.Key.From, e.Key.To, e.Value.ClusterWeight)).ToList();
        var consensus = ModuleConsensus.Cluster(ui, graph.Nodes.ToDictionary(p => p.Key, p => p.Value.Home), edges);

        var module = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var moduleLayer = new SortedDictionary<string, Layer>(StringComparer.Ordinal);
        var clustered = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (id, number) in consensus.Module)
        {
            var key = KeyPrefix + number;
            module[id] = key;
            moduleLayer[key] = Layer.UI;
            clustered.Add(key);
        }

        foreach (var id in consensus.Loose) moduleLayer[ModuleAssignment.LooseKey(id)] = Layer.UI;
        return new UiModuleAssignment(module, consensus.Loose, moduleLayer,
            LayeredModules.Names(graph, module, moduleLayer, clustered));
    }
}
