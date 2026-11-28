using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture graphs for the UI view of the dependency gate: the UI layer seen from inside (#484).</summary>
[TestSuite]
public class UiModulesTest
{
    [TestCase]
    public static void TwoDenseGroupsOfUiClassesAreTwoUiModulesWithNoLoop()
    {
        var measurement = GateMetrics.Measure(TwoUiGroups());

        AssertThat(ModulesOf(measurement, "P0", "P1", "P2", "P3", "P4")).IsEqual("U0");
        AssertThat(ModulesOf(measurement, "Q0", "Q1", "Q2", "Q3", "Q4")).IsEqual("U1");
        AssertThat(measurement.Ui.Modules.Loose.Count).IsEqual(0);
        AssertThat(measurement.Gated.UiUpEdges).IsEqual(0);
        AssertThat(measurement.Gated.UiBiggestLoop).IsEqual(1);
    }

    [TestCase]
    public static void ALoopBetweenUiModulesMovesOnlyTheTwoUiNumbers()
    {
        var graph = TwoUiGroups();
        graph.AddEdge("P0", "Q0", EdgeKind.Calls);
        graph.AddEdge("Q1", "P1", EdgeKind.Calls);

        var gated = GateMetrics.Measure(graph).Gated;

        AssertThat(gated.UiUpEdges).IsEqual(1);
        AssertThat(gated.UiBiggestLoop).IsEqual(2);
        AssertThat(gated.UpEdges).IsEqual(0);
        AssertThat(gated.BiggestLoop).IsEqual(1);
        AssertThat(gated.SeamsLeakyOrMissing).IsEqual(0);
        AssertThat(gated.RefsFromNonUiToUi).IsEqual(0);
    }

    [TestCase]
    public static void ALooseUiClassIsItsOwnUiModuleAndCountsInTheLoop()
    {
        var graph = TwoUiGroups();
        graph.AddNode("Glue", NodeRole.Concrete, uiSeed: true);
        graph.AddEdge("P0", "Glue", EdgeKind.Calls);
        graph.AddEdge("Glue", "P1", EdgeKind.Calls);

        var measurement = GateMetrics.Measure(graph);

        AssertThat(measurement.Ui.Modules.Loose.Contains("Glue")).IsTrue();
        AssertThat(measurement.Ui.Modules.SpreadModule("Glue")).IsEqual("loose Glue");
        AssertThat(measurement.Gated.UiBiggestLoop).IsEqual(2);
        AssertThat(string.Join(",", measurement.Lists.UiLoopClasses)).IsEqual("Glue,P0,P1,P2,P3,P4");
        AssertThat(measurement.Lists.UiUpEdgePairs.Count > 0).IsTrue();
    }

    [TestCase]
    public static void ReportNamesTheUiModulesTheirUpEdgesAndLoops()
    {
        var graph = TwoUiGroups();
        graph.AddEdge("P0", "Q0", EdgeKind.Calls);
        graph.AddEdge("Q1", "P1", EdgeKind.Calls);

        var lines = GateReport.Lines(GateMetrics.Measure(graph));

        AssertThat(lines.Any(l => l.EndsWith("ui_up_edges 1, ui_biggest_loop 2", System.StringComparison.Ordinal)))
            .IsTrue();
        AssertThat(lines.Contains("ui modules 2, loose 0")).IsTrue();
        AssertThat(lines.Contains("ui order 1: U1 Q0 (5 classes)")).IsTrue();
        AssertThat(lines.Contains("ui order 2: U0 P0 (5 classes)")).IsTrue();
        AssertThat(lines.Contains("ui up edge: U1 Q0 -> U0 P0 weight 1: Q1 -> P1 1")).IsTrue();
        AssertThat(lines.Contains("ui loop of 2: U0 P0, U1 Q0")).IsTrue();
    }

    [TestCase]
    public static void AGraphWithoutUiClassesHasNoUiModulesAndNoUiLoop()
    {
        var graph = new ClassGraph();
        graph.AddNode("Element", NodeRole.Data);
        graph.AddNode("Signature", NodeRole.Data);
        graph.AddEdge("Signature", "Element", EdgeKind.MemberType);

        var measurement = GateMetrics.Measure(graph);

        AssertThat(measurement.Ui.Modules.Module.Count).IsEqual(0);
        AssertThat(measurement.Gated.UiUpEdges).IsEqual(0);
        AssertThat(measurement.Gated.UiBiggestLoop).IsEqual(1);
    }

    private static string ModulesOf(GateMeasurement measurement, params string[] ids) =>
        string.Join(",", ids.Select(id => measurement.Ui.Modules.Module[id]).Distinct());

    // Two groups of five UI classes, each group fully connected inside and unconnected to the other.
    private static ClassGraph TwoUiGroups()
    {
        var graph = new ClassGraph();
        foreach (var prefix in new[] { "P", "Q" })
        {
            for (var i = 0; i < 5; i++) graph.AddNode($"{prefix}{i}", NodeRole.Concrete, uiSeed: true);
            for (var a = 0; a < 5; a++)
            {
                for (var b = 0; b < 5; b++) graph.AddEdge($"{prefix}{a}", $"{prefix}{b}", EdgeKind.Calls);
            }
        }

        return graph;
    }
}
