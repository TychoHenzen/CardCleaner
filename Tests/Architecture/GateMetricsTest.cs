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
