using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// One measurement of the layered module structure: the modules, their order and edges, and the seams.
/// </summary>
internal sealed record GateMeasurement(
    ClassGraph Graph,
    ModuleAssignment Modules,
    ModuleOrderResult Order,
    SortedDictionary<EdgeKey, ModuleEdge> SpreadEdges,
    List<SeamRating> Seams,
    int RefsFromNonUiToUi)
{
    public GatedNumbers Gated => new(Order.UpEdges.Count, Order.BiggestLoop,
        SeamCount(SeamKind.Leaky) + SeamCount(SeamKind.Missing), RefsFromNonUiToUi);

    /// <summary>The class names behind each gated number: diagnostics only, never gated.</summary>
    public NamedLists Lists
    {
        get
        {
            var loop = Order.Loops.Count == 0
                ? new HashSet<string>(StringComparer.Ordinal)
                : Order.Loops[0].ToHashSet(StringComparer.Ordinal);
            return new NamedLists(
                Sorted(Order.UpEdges.SelectMany(k => SpreadEdges[k].Pairs.Select(p => $"{p.From} -> {p.To}"))),
                Sorted(Graph.Nodes.Keys.Where(id => loop.Contains(Modules.SpreadModule(id)))),
                Sorted(Seams.Where(s => s.Rating != SeamKind.Defined).SelectMany(s => s.Concrete)),
                Sorted(Graph.Edges.Keys
                    .Where(k => Modules.Layers[k.From] != Layer.UI && Modules.Layers[k.To] == Layer.UI)
                    .Select(k => $"{k.From} -> {k.To}")));
        }
    }

    public int SeamCount(SeamKind rating) => Seams.Count(s => s.Rating == rating);

    private static List<string> Sorted(IEnumerable<string> items) =>
        items.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();

    public int LayerCount(Layer layer) => Modules.Layers.Values.Count(l => l == layer);

    public int RealModuleCount => Modules.Module.Values.Distinct().Count();
}
