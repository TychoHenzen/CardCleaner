using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Louvain community detection (Blondel local moving and aggregation), a port of networkx 3.6
/// louvain_communities with threshold 1e-7. The node order of each level is shuffled by one System.Random(seed),
/// and every other collection is iterated in ascending index order, so a seed always gives the same partition.
/// </summary>
internal static class LouvainClustering
{
    private const double Threshold = 1e-7;

    /// <summary>Communities of nodes 0..nodeCount-1, each sorted ascending, in community-id order.</summary>
    public static List<List<int>> Communities(int nodeCount, IReadOnlyList<(int A, int B, double W)> edges,
        double resolution, int seed)
    {
        var adjacency = Adjacency(nodeCount, edges);
        var m = edges.Sum(e => e.W);
        if (m <= 0) return Enumerable.Range(0, nodeCount).Select(i => new List<int> { i }).ToList();

        var random = new Random(seed);
        var members = Enumerable.Range(0, nodeCount).Select(i => new List<int> { i }).ToList();
        var singletons = Enumerable.Range(0, nodeCount).Select(i => new List<int> { i }).ToList();
        var modularity = Modularity(adjacency, singletons, m, resolution);
        var level = OneLevel(adjacency, m, members, resolution, random);
        while (true)
        {
            var last = level.Members;
            var newModularity = Modularity(adjacency, level.Inner, m, resolution);
            if (newModularity - modularity <= Threshold || !level.Improved) return Sorted(last);
            modularity = newModularity;
            adjacency = Aggregate(adjacency, level.Inner);
            level = OneLevel(adjacency, m, last, resolution, random);
            if (!level.Improved) return Sorted(last);
        }
    }

    private sealed record Level(List<List<int>> Members, List<List<int>> Inner, bool Improved);

    private static Level OneLevel(SortedDictionary<int, double>[] adjacency, double m, List<List<int>> members,
        double resolution, Random random)
    {
        var count = adjacency.Length;
        var nodeToCommunity = Enumerable.Range(0, count).ToArray();
        var inner = Enumerable.Range(0, count).Select(i => new List<int> { i }).ToList();
        var communityMembers = members.Select(c => new List<int>(c)).ToList();
        var degrees = Enumerable.Range(0, count).Select(i => Degree(adjacency, i)).ToArray();
        var totals = (double[])degrees.Clone();
        var order = Enumerable.Range(0, count).ToArray();
        for (var i = count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }

        var improved = false;
        var moves = 1;
        var scale = 2 * m * m;
        while (moves > 0)
        {
            moves = 0;
            foreach (var u in order)
            {
                var current = nodeToCommunity[u];
                var weights = NeighbourCommunityWeights(adjacency[u], u, nodeToCommunity);
                var degree = degrees[u];
                totals[current] -= degree;
                var removeCost = -WeightOf(weights, current) / m + resolution * totals[current] * degree / scale;
                var best = current;
                var bestGain = 0.0;
                foreach (var (community, weight) in weights)
                {
                    var gain = removeCost + weight / m - resolution * totals[community] * degree / scale;
                    if (gain <= bestGain) continue;
                    bestGain = gain;
                    best = community;
                }

                totals[best] += degree;
                if (best == current) continue;
                communityMembers[best].AddRange(members[u]);
                communityMembers[current].RemoveAll(members[u].Contains);
                inner[current].Remove(u);
                inner[best].Add(u);
                nodeToCommunity[u] = best;
                improved = true;
                moves++;
            }
        }

        return new Level(communityMembers.Where(c => c.Count > 0).ToList(), inner.Where(c => c.Count > 0).ToList(),
            improved);
    }

    /// <summary>Neighbour weights summed per community, in first-seen order of ascending neighbour index.</summary>
    private static List<(int Community, double Weight)> NeighbourCommunityWeights(
        SortedDictionary<int, double> neighbours, int u, int[] nodeToCommunity)
    {
        var result = new List<(int Community, double Weight)>();
        var index = new Dictionary<int, int>();
        foreach (var (v, weight) in neighbours)
        {
            if (v == u) continue;
            var community = nodeToCommunity[v];
            if (index.TryGetValue(community, out var at))
            {
                result[at] = (community, result[at].Weight + weight);
                continue;
            }

            index[community] = result.Count;
            result.Add((community, weight));
        }

        return result;
    }

    private static double WeightOf(List<(int Community, double Weight)> weights, int community)
    {
        foreach (var (c, w) in weights)
        {
            if (c == community) return w;
        }

        return 0;
    }

    private static double Degree(SortedDictionary<int, double>[] adjacency, int i) =>
        adjacency[i].Sum(p => p.Key == i ? 2 * p.Value : p.Value);

    private static SortedDictionary<int, double>[] Adjacency(int nodeCount,
        IReadOnlyList<(int A, int B, double W)> edges)
    {
        var adjacency = Enumerable.Range(0, nodeCount).Select(_ => new SortedDictionary<int, double>()).ToArray();
        foreach (var (a, b, w) in edges) AddWeight(adjacency, a, b, w);
        return adjacency;
    }

    private static void AddWeight(SortedDictionary<int, double>[] adjacency, int a, int b, double w)
    {
        adjacency[a][b] = adjacency[a].GetValueOrDefault(b) + w;
        if (a != b) adjacency[b][a] = adjacency[b].GetValueOrDefault(a) + w;
    }

    /// <summary>Sum over communities of Lc/m - resolution * (degc / 2m)^2; each edge and self loop counted once.</summary>
    private static double Modularity(SortedDictionary<int, double>[] adjacency, List<List<int>> communities, double m,
        double resolution)
    {
        var result = 0.0;
        foreach (var community in communities)
        {
            var set = new HashSet<int>(community);
            var inside = 0.0;
            var degree = 0.0;
            foreach (var u in community)
            {
                degree += Degree(adjacency, u);
                foreach (var (v, w) in adjacency[u])
                {
                    if (set.Contains(v) && v >= u) inside += w;
                }
            }

            result += inside / m - resolution * Math.Pow(degree / (2 * m), 2);
        }

        return result;
    }

    /// <summary>One node per community; weights summed, edges inside a community become self loops.</summary>
    private static SortedDictionary<int, double>[] Aggregate(SortedDictionary<int, double>[] adjacency,
        List<List<int>> communities)
    {
        var owner = new int[adjacency.Length];
        for (var c = 0; c < communities.Count; c++)
        {
            foreach (var u in communities[c]) owner[u] = c;
        }

        var result = Enumerable.Range(0, communities.Count).Select(_ => new SortedDictionary<int, double>()).ToArray();
        for (var u = 0; u < adjacency.Length; u++)
        {
            foreach (var (v, w) in adjacency[u])
            {
                if (v >= u) AddWeight(result, owner[u], owner[v], w);
            }
        }

        return result;
    }

    private static List<List<int>> Sorted(List<List<int>> communities) =>
        communities.Select(c => c.OrderBy(i => i).ToList()).ToList();
}
