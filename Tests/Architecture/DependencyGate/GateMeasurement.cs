using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>One measurement of the layered module structure: the modules, their order and edges, and the seams.</summary>
internal sealed record GateMeasurement(
    ClassGraph Graph,
    ModuleAssignment Modules,
    ModuleOrderResult Order,
    SortedDictionary<EdgeKey, ModuleEdge> SpreadEdges,
    List<SeamRating> Seams,
    int RefsFromNonUiToUi)
{
    public GatedNumbers Gated => new(Order.UpEdges.Count, Order.BiggestLoop, SeamCount(SeamRating.Leaky),
        SeamCount(SeamRating.Missing), RefsFromNonUiToUi);

    public int SeamCount(string rating) => Seams.Count(s => s.Rating == rating);

    public int LayerCount(Layer layer) => Modules.Layers.Values.Count(l => l == layer);

    public int RealModuleCount => Modules.Module.Values.Distinct().Count();
}
