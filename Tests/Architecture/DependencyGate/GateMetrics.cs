using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>Measures a class graph: layers, modules, order, up edges, loops, seams and references into UI.</summary>
internal static class GateMetrics
{
    public static GateMeasurement Measure(ClassGraph graph)
    {
        var modules = LayeredModules.Assign(graph);
        var spread = ModuleEdges.Build(graph, modules.SpreadModule);
        var real = ModuleEdges.Build(graph, id => modules.Module.GetValueOrDefault(id));
        var order = ModuleOrder.Order(modules.ModuleLayer, spread);
        var seams = SeamRater.Rate(real, graph.Nodes);
        var toUi = graph.Edges.Keys.Count(k => modules.Layers[k.From] != Layer.UI && modules.Layers[k.To] == Layer.UI);
        return new GateMeasurement(graph, modules, order, spread, seams, toUi);
    }
}
