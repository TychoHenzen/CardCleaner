using System.Collections.Generic;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture module graphs for the layered order, up edges and loops of the dependency gate (#240 AC-6, AC-7).</summary>
[TestSuite]
public class ModuleOrderTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TwoModuleDagHasNoUpEdge()
    {
        var result = ModuleOrder.Order(Middle("A", "B"), Edges(("A", "B", 3)));

        AssertThat(string.Join(",", result.Order)).IsEqual("B,A");
        AssertThat(result.UpEdges.Count).IsEqual(0);
        AssertThat(result.BiggestLoop).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TwoModuleCycleIsALoopOfTwoWithOneUpEdge()
    {
        var result = ModuleOrder.Order(Middle("A", "B"), Edges(("A", "B", 5), ("B", "A", 1)));

        AssertThat(result.BiggestLoop).IsEqual(2);
        AssertThat(result.UpEdges.Count).IsEqual(1);
        AssertThat(result.UpEdges[0].ToString()).IsEqual(new EdgeKey("B", "A").ToString());
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LayersNeverInterleaveSoMiddleToUiIsUp()
    {
        var layers = new Dictionary<string, Layer> { ["Data"] = Layer.Data, ["M0"] = Layer.Middle, ["UI"] = Layer.UI };

        var result = ModuleOrder.Order(layers, Edges(("M0", "UI", 1), ("UI", "M0", 1), ("M0", "Data", 1)));

        AssertThat(string.Join(",", result.Order)).IsEqual("Data,M0,UI");
        AssertThat(result.UpEdges.Count).IsEqual(1);
        AssertThat(result.UpEdges[0].ToString()).IsEqual(new EdgeKey("M0", "UI").ToString());
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LoopThroughLooseClassesCountsAsALoopOfTwo()
    {
        var graph = new ClassGraph();
        foreach (var prefix in new[] { "P", "Q" })
        {
            for (var i = 0; i < 5; i++) graph.AddNode($"{prefix}{i}", NodeRole.Concrete);
            for (var a = 0; a < 5; a++)
            {
                for (var b = 0; b < 5; b++) graph.AddEdge($"{prefix}{a}", $"{prefix}{b}", EdgeKind.Calls);
            }
        }

        graph.AddNode("IShape", NodeRole.Interface);
        graph.AddNode("Shape", NodeRole.Concrete);
        graph.AddEdge("IShape", "Shape", EdgeKind.Creates);
        graph.AddEdge("Shape", "IShape", EdgeKind.Inherits);

        var measurement = GateMetrics.Measure(graph);

        AssertThat(measurement.Modules.Loose.Contains("IShape")).IsTrue();
        AssertThat(measurement.Modules.Loose.Contains("Shape")).IsTrue();
        AssertThat(measurement.Gated.BiggestLoop).IsEqual(2);
        AssertThat(measurement.Gated.UpEdges).IsEqual(1);
    }

    private static Dictionary<string, Layer> Middle(params string[] modules)
    {
        var layers = new Dictionary<string, Layer>();
        foreach (var module in modules) layers[module] = Layer.Middle;
        return layers;
    }

    private static SortedDictionary<EdgeKey, ModuleEdge> Edges(params (string From, string To, long Weight)[] edges)
    {
        var result = new SortedDictionary<EdgeKey, ModuleEdge>();
        foreach (var (from, to, weight) in edges)
        {
            var edge = new ModuleEdge { Weight = weight };
            edge.Pairs.Add(($"{from}.x", $"{to}.y", weight));
            result[new EdgeKey(from, to)] = edge;
        }

        return result;
    }
}
