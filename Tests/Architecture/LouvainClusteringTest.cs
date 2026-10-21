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
        var communities = LouvainClustering.Communities(Keys(10), TwoCliques(),1.0, 0);

        AssertThat(Describe(communities)).IsEqual("0,1,2,3,4|5,6,7,8,9");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheSameSeedGivesTheSamePartition()
    {
        for (var seed = 0; seed < 10; seed++)
        {
            var first = LouvainClustering.Communities(Keys(10), TwoCliques(),0.7, seed);
            var second = LouvainClustering.Communities(Keys(10), TwoCliques(),0.7, seed);
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

    [TestCase]
    [TestCategory("Unit")]
    public static void AddingAClassThatSortsFirstKeepsTheCliquesOfTheOthers()
    {
        var names = new List<string>();
        foreach (var prefix in new[] { "A", "M", "Z" }) names.AddRange(Enumerable.Range(0, 5).Select(i => prefix + i));
        var before = ClusterNamed(names, CliqueEdges(names));

        var added = new List<string>(names) { "AAA" };
        var edges = CliqueEdges(names);
        foreach (var member in new[] { "A0", "A1", "A2" })
        {
            edges.Add(("AAA", member, 3.0));
            edges.Add((member, "AAA", 3.0));
        }

        var after = ClusterNamed(added, edges);

        AssertThat(before.Loose.Count).IsEqual(0);
        AssertThat(after.Loose.Any(names.Contains)).IsFalse();
        AssertThat(SameModulePairs(after, names)).IsEqual(SameModulePairs(before, names));
    }

    /// <summary>Three cliques of five classes each (weight 3.0), joined by light bridges A4-M0 and M4-Z0 (weight 1.0).</summary>
    private static List<(string From, string To, double Cw)> CliqueEdges(IReadOnlyList<string> names)
    {
        var edges = new List<(string From, string To, double Cw)>();
        for (var clique = 0; clique < 3; clique++)
        {
            for (var a = 0; a < 5; a++)
            {
                for (var b = 0; b < 5; b++)
                {
                    if (a != b) edges.Add((names[clique * 5 + a], names[clique * 5 + b], 3.0));
                }
            }
        }

        edges.AddRange([(names[4], names[5], 1.0), (names[5], names[4], 1.0)]);
        edges.AddRange([(names[9], names[10], 1.0), (names[10], names[9], 1.0)]);
        return edges;
    }

    /// <summary>Clusters the named classes, each in a home of its own, as the Cluster helper does.</summary>
    private static ConsensusResult ClusterNamed(IReadOnlyList<string> names,
        List<(string From, string To, double Cw)> edges)
    {
        var nodes = names.OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        return ModuleConsensus.Cluster(nodes, nodes.ToDictionary(n => n, n => "Features/" + n), edges);
    }

    /// <summary>One character per pair of the given classes: 1 when both are in the same module, else 0.</summary>
    private static string SameModulePairs(ConsensusResult result, IReadOnlyList<string> names)
    {
        var bits = new System.Text.StringBuilder();
        for (var x = 0; x < names.Count; x++)
        {
            for (var y = x + 1; y < names.Count; y++)
            {
                bits.Append(result.Module[names[x]] == result.Module[names[y]] ? '1' : '0');
            }
        }

        return bits.ToString();
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

    /// <summary>Zero padded names N00..N(n-1), so ordinal order equals index order.</summary>
    private static List<string> Keys(int n) => Enumerable.Range(0, n).Select(i => $"N{i:D2}").ToList();

    private static string Describe(List<List<int>> communities) =>
        string.Join("|", communities.Select(c => string.Join(",", c)).OrderBy(s => s, System.StringComparer.Ordinal));
}
