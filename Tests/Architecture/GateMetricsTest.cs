using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>
/// Fixture graph for the anchor modules and the references into UI of the dependency gate (#240 AC-6).
/// </summary>
[TestSuite]
public class GateMetricsTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void EachAnchorLayerIsOneModule()
    {
        var measurement = GateMetrics.Measure(LayeredGraph());

        AssertThat(ModulesOf(measurement, "Button", "Panel")).IsEqual("UI");
        AssertThat(ModulesOf(measurement, "Element", "Signature")).IsEqual("Data");
        AssertThat(ModulesOf(measurement, "IReader", "IWriter")).IsEqual("Contracts");
        AssertThat(string.Join(",", measurement.Order.Order)).IsEqual("Data,Contracts,loose Rules,UI");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void MiddleClassReferencingUiCountsAsReferenceIntoUi()
    {
        var measurement = GateMetrics.Measure(LayeredGraph());

        AssertThat(measurement.Gated.RefsFromNonUiToUi).IsEqual(1);
        AssertThat(measurement.Gated.UpEdges).IsEqual(1);
        var upIntoUi = GateReport.Lines(measurement)
            .Any(l => l.StartsWith("up edge: loose Rules -> UI", System.StringComparison.Ordinal));
        AssertThat(upIntoUi).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ListsNameTheClassesBehindTheGatedNumbers()
    {
        var lists = GateMetrics.Measure(LayeredGraph()).Lists;

        AssertThat(string.Join(",", lists.UiRefPairs)).IsEqual("Rules -> Button");
        AssertThat(lists.UpEdgePairs.Count > 0).IsTrue();
        // Panel -> Rules and Rules -> Button close a loop between the UI module and the loose Rules module.
        AssertThat(string.Join(",", lists.BiggestLoopClasses)).IsEqual("Button,Panel,Rules");
    }

    /// <summary>
    /// Pins that the gated seam number is leaky plus missing, so the label of one seam can flip with Louvain membership
    /// (#278) without moving the gated number, while a second defective seam still raises it by one.
    /// </summary>
    [TestCase]
    [TestCategory("Unit")]
    public static void GatedSeamCountIgnoresWhetherASeamIsRatedMissingOrLeaky()
    {
        var measurement = GateMetrics.Measure(LayeredGraph());
        var panelToRules = new EdgeKey("Panel", "Rules");
        var rulesToButton = new EdgeKey("Rules", "Button");

        var missing = measurement with { Seams = [Seam(panelToRules, SeamKind.Missing)] };
        var leaky = measurement with { Seams = [Seam(panelToRules, SeamKind.Leaky)] };
        var twoDefects = measurement with
        {
            Seams = [Seam(panelToRules, SeamKind.Missing), Seam(rulesToButton, SeamKind.Leaky)],
        };

        AssertThat(missing.Gated.SeamsLeakyOrMissing).IsEqual(1);
        AssertThat(leaky.Gated.SeamsLeakyOrMissing).IsEqual(missing.Gated.SeamsLeakyOrMissing);
        AssertThat(twoDefects.Gated.SeamsLeakyOrMissing).IsEqual(2);
    }

    private static SeamRating Seam(EdgeKey modules, SeamKind rating) => new(modules, rating, 0.5, [], false);

    private static string ModulesOf(GateMeasurement measurement, params string[] ids) =>
        string.Join(",", ids.Select(id => measurement.Modules.Module[id]).Distinct());

    private static ClassGraph LayeredGraph()
    {
        var graph = new ClassGraph();
        graph.AddNode("Button", NodeRole.Concrete, uiSeed: true);
        graph.AddNode("Panel", NodeRole.Concrete, uiSeed: true);
        graph.AddNode("Element", NodeRole.Data);
        graph.AddNode("Signature", NodeRole.Data);
        graph.AddNode("IReader", NodeRole.Interface);
        graph.AddNode("IWriter", NodeRole.Interface);
        graph.AddNode("Rules", NodeRole.Concrete);
        graph.AddEdge("Panel", "Button", EdgeKind.MemberType);
        graph.AddEdge("Panel", "Rules", EdgeKind.Calls);
        graph.AddEdge("Signature", "Element", EdgeKind.MemberType);
        graph.AddEdge("IReader", "Signature", EdgeKind.MemberType);
        graph.AddEdge("IWriter", "IReader", EdgeKind.MemberType);
        graph.AddEdge("Rules", "IWriter", EdgeKind.Calls);
        graph.AddEdge("Rules", "Button", EdgeKind.Calls);
        return graph;
    }
}
