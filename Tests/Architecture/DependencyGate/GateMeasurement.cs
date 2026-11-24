using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// One measurement of the layered module structure: the modules, their order and edges, the seams, and the UI layer
/// seen from inside.
/// </summary>
internal sealed record GateMeasurement(
    ClassGraph Graph,
    ModuleAssignment Modules,
    ModuleOrderResult Order,
    SortedDictionary<EdgeKey, ModuleEdge> SpreadEdges,
    List<SeamRating> Seams,
    int RefsFromNonUiToUi,
    UiView Ui)
{
    public GatedNumbers Gated => new(Order.UpEdges.Count, Order.BiggestLoop,
        SeamCount(SeamKind.Leaky) + SeamCount(SeamKind.Missing), RefsFromNonUiToUi, Ui.Order.UpEdges.Count,
        Ui.Order.BiggestLoop);

    /// <summary>The class names behind each gated number: diagnostics only, never gated.</summary>
    public NamedLists Lists => new(
        Sorted(UpEdgePairs(Order, SpreadEdges)),
        Sorted(ClassesInBiggestLoop(Order, Modules.SpreadModule)),
        Sorted(Seams.Where(s => s.Rating != SeamKind.Defined).SelectMany(s => s.Concrete)),
        Sorted(Graph.Edges.Keys
            .Where(k => Modules.Layers[k.From] != Layer.UI && Modules.Layers[k.To] == Layer.UI)
            .Select(k => $"{k.From} -> {k.To}")),
        Sorted(UpEdgePairs(Ui.Order, Ui.Edges)),
        Sorted(ClassesInBiggestLoop(Ui.Order, Ui.Modules.SpreadModule)));

    public int SeamCount(SeamKind rating) => Seams.Count(s => s.Rating == rating);

    private static List<string> Sorted(IEnumerable<string> items) =>
        items.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static IEnumerable<string> UpEdgePairs(ModuleOrderResult order,
        SortedDictionary<EdgeKey, ModuleEdge> edges) =>
        order.UpEdges.SelectMany(k => edges[k].Pairs.Select(p => $"{p.From} -> {p.To}"));

    private IEnumerable<string> ClassesInBiggestLoop(ModuleOrderResult order, Func<string, string?> moduleOf)
    {
        if (order.Loops.Count == 0) return [];
        var loop = order.Loops[0].ToHashSet(StringComparer.Ordinal);
        return Graph.Nodes.Keys.Where(id => moduleOf(id) is { } module && loop.Contains(module));
    }

    public int LayerCount(Layer layer) => Modules.Layers.Values.Count(l => l == layer);

    public int RealModuleCount => Modules.Module.Values.Distinct().Count();
}
