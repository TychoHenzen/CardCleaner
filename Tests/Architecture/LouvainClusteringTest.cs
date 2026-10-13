using System.Collections.Generic;
using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>
/// Fixture graphs for the Louvain port and the consensus clustering of the dependency gate (#240 AC-7).
/// </summary>
[TestSuite]
public class LouvainClusteringTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TwoCliquesJoinedByOneEdgeGiveTwoCommunities()
    {
        var communities = LouvainClustering.Communities(10, TwoCliques(), 1.0, 0);

        AssertThat(Describe(communities)).IsEqual("0,1,2,3,4|5,6,7,8,9");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheSameSeedGivesTheSamePartition()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var first = LouvainClustering.Communities(10, TwoCliques(), 0.7, seed);
            var second = LouvainClustering.Communities(10, TwoCliques(), 0.7, seed);
            AssertThat(Describe(second)).IsEqual(Describe(first));
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ConsensusOfTwoCliquesGivesTwoModules()
    {
        var names = Enumerable.Range(0, 10).Select(i => $"C{i}").ToList();
        var home = names.ToDictionary(n => n, _ => "Features/X");
        var edges = TwoCliques().SelectMany(e => new[]
            { (names[e.A], names[e.B], e.W), (names[e.B], names[e.A], e.W) }).ToList();

        var result = ModuleConsensus.Cluster(names, home, edges);

        AssertThat(result.Loose.Count).IsEqual(0);
        AssertThat(result.Module.Values.Distinct().Count()).IsEqual(2);
        AssertThat(names.Take(5).Select(n => result.Module[n]).Distinct().Count()).IsEqual(1);
        AssertThat(names.Skip(5).Select(n => result.Module[n]).Distinct().Count()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ClassReferencedFromSixHomesIsOmnipresentAndFromFiveIsNot()
    {
        var edges = NamedCliques();
        foreach (var user in new[] { "A0", "A1", "A2", "B0", "B1", "B2" }) edges.Add((user, "Six", 1.0));
        foreach (var user in new[] { "A0", "A1", "A2", "A3", "A4" }) edges.Add((user, "Five", 1.0));

        var result = Cluster(edges);

        AssertThat(result.Loose.Contains("Six")).IsTrue();
        AssertThat(result.Module["Five"]).IsEqual(result.Module["A0"]);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void OmnipresentClassJoinsTheModuleItReferencesMost()
    {
        var edges = NamedCliques();
        foreach (var user in new[] { "A0", "A1", "A2", "B0", "B1", "B2" }) edges.Add((user, "Omni", 1.0));
        edges.AddRange([("Omni", "A0", 1.0), ("Omni", "A1", 1.0), ("Omni", "B1", 1.0)]);

        var result = Cluster(edges);

        AssertThat(result.Loose.Contains("Omni")).IsFalse();
        AssertThat(result.Module["Omni"]).IsEqual(result.Module["A0"]);
        AssertThat(result.Module["A0"]).IsNotEqual(result.Module["B0"]);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ClassUnderTheLooseDegreeGoesLooseAndOneAtItIsClustered()
    {
        var edges = NamedCliques();
        edges.Add(("A0", "Weak", 3.9));
        edges.AddRange([("A0", "Tied", 2.0), ("Tied", "A1", 2.0)]);

        var result = Cluster(edges);

        AssertThat(result.Loose.Contains("Weak")).IsTrue();
        AssertThat(result.Module["Tied"]).IsEqual(result.Module["A0"]);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CommunityOfThreeGoesLoose()
    {
        var edges = NamedCliques();
        edges.AddRange([("T0", "T1", 3.0), ("T1", "T2", 3.0), ("T2", "T0", 3.0)]);

        var result = Cluster(edges);

        AssertThat(string.Join(",", result.Loose)).IsEqual("T0,T1,T2");
        AssertThat(result.Module.Values.Distinct().Count()).IsEqual(2);
    }

    /// <summary>The two cliques as classes A0..A4 and B0..B4, referenced both ways.</summary>
    private static List<(string From, string To, double Cw)> NamedCliques()
    {
        var edges = new List<(string From, string To, double Cw)>();
        foreach (var (a, b, w) in TwoCliques()) edges.AddRange([(Named(a), Named(b), w), (Named(b), Named(a), w)]);
        return edges;
    }

    private static string Named(int i) => i < 5 ? $"A{i}" : $"B{i - 5}";

    /// <summary>Clusters every class of the edges, each class in a home of its own.</summary>
    private static ConsensusResult Cluster(List<(string From, string To, double Cw)> edges)
    {
        var nodes = edges.SelectMany(e => new[] { e.From, e.To }).Distinct()
            .OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        return ModuleConsensus.Cluster(nodes, nodes.ToDictionary(n => n, n => "Features/" + n), edges);
    }

    private static List<(int A, int B, double W)> TwoCliques()
    {
        var edges = new List<(int A, int B, double W)>();
        foreach (var offset in new[] { 0, 5 })
        {
            for (var a = 0; a < 5; a++)
            {
                for (var b = a + 1; b < 5; b++) edges.Add((offset + a, offset + b, 1.0));
            }
        }

        edges.Add((4, 5, 1.0));
        return edges;
    }

    private static string Describe(List<List<int>> communities) =>
        string.Join("|", communities.Select(c => string.Join(",", c)).OrderBy(s => s, System.StringComparer.Ordinal));
}
