using System.Collections.Generic;
using System.Linq;
using CardCleaner.Tests.Architecture.DependencyGate;

namespace CardCleaner.Tests.Architecture;

/// <summary>Fixture graphs for the Louvain port and the consensus clustering of the dependency gate (#240 AC-7).</summary>
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
