using System.Collections.Generic;
using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture seams for the defined, leaky and missing ratings of the dependency gate (#240 AC-7).</summary>
[TestSuite]
public class SeamRaterTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void ContractsOnlySeamIsDefined()
    {
        var edges = Edges(("X", "Y", [("a", "IPort", 1), ("b", "Record", 9)]));

        AssertThat(RatingOf(edges, "X", "Y")).IsEqual(SeamRating.Defined);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CrossedConcreteClassIsLeaky()
    {
        var edges = Edges(("X", "Y", [("a", "IPort", 5), ("b", "Engine", 1)]));

        var seam = SeamRater.Rate(edges, Nodes()).Single();

        AssertThat(seam.Rating).IsEqual(SeamRating.Leaky);
        AssertThat(string.Join(",", seam.Concrete)).IsEqual("Engine");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ConcreteCallbackMakesTheSeamLeaky()
    {
        var edges = Edges(("X", "Y", [("a", "IPort", 1)]), ("Y", "X", [("IPort", "a", 1)]));

        AssertThat(RatingOf(edges, "X", "Y")).IsEqual(SeamRating.Leaky);
        AssertThat(RatingOf(edges, "Y", "X")).IsEqual(SeamRating.Leaky);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SpreadOverManyConcreteClassesIsMissing()
    {
        var spread = Enumerable.Range(0, 6).Select(i => ("a", $"C{i}", 1L)).ToArray();
        var fiveSpread = Enumerable.Range(0, 5).Select(i => ("a", $"C{i}", 1L)).ToArray();

        AssertThat(RatingOf(Edges(("X", "Y", spread)), "X", "Y")).IsEqual(SeamRating.Missing);
        AssertThat(RatingOf(Edges(("X", "Y", fiveSpread)), "X", "Y")).IsEqual(SeamRating.Leaky);
    }

    private static string RatingOf(SortedDictionary<EdgeKey, ModuleEdge> edges, string from, string to) =>
        SeamRater.Rate(edges, Nodes()).Single(s => s.Modules.From == from && s.Modules.To == to).Rating;

    private static Dictionary<string, GateNode> Nodes()
    {
        var nodes = new Dictionary<string, GateNode>
        {
            ["a"] = new("a", NodeRole.Concrete, "X", false),
            ["b"] = new("b", NodeRole.Concrete, "X", false),
            ["IPort"] = new("IPort", NodeRole.Interface, "Y", false),
            ["Record"] = new("Record", NodeRole.Data, "Y", false),
            ["Engine"] = new("Engine", NodeRole.Concrete, "Y", false),
        };
        for (var i = 0; i < 6; i++) nodes[$"C{i}"] = new($"C{i}", NodeRole.Concrete, "Y", false);
        return nodes;
    }

    private static SortedDictionary<EdgeKey, ModuleEdge> Edges(
        params (string From, string To, (string A, string B, long W)[] Pairs)[] edges)
    {
        var result = new SortedDictionary<EdgeKey, ModuleEdge>();
        foreach (var (from, to, pairs) in edges)
        {
            var edge = new ModuleEdge { Weight = pairs.Sum(p => p.W) };
            edge.Pairs.AddRange(pairs);
            result[new EdgeKey(from, to)] = edge;
        }

        return result;
    }
}
