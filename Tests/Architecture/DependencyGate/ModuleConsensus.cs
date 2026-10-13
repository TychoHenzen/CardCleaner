using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Consensus clustering of the middle layer (#240 requirement 6): omnipresent and loose classes are left out,
/// Louvain at resolution 0.7 runs with seeds 0 to 49, and pairs together in at least half the runs form a consensus
/// graph clustered at resolution 1.0, seed 0.
/// </summary>
internal static class ModuleConsensus
{
    private const int OmniHomes = 6;
    private const double LooseDegree = 4;
    private const int Runs = 50;
    private const double RunResolution = 0.7;
    private const int MinModule = 4;

    public static ConsensusResult Cluster(IReadOnlyList<string> nodes, IReadOnlyDictionary<string, string> home,
        IReadOnlyList<(string From, string To, double Cw)> edges)
    {
        var omni = Omnipresent(home, edges);
        var undirected = Undirected(edges, omni);
        var loose = new SortedSet<string>(StringComparer.Ordinal);
        var clustered = new List<string>();
        foreach (var node in nodes.Where(n => !omni.Contains(n)).OrderBy(n => n, StringComparer.Ordinal))
        {
            var degree = undirected.Where(e => e.Key.From == node || e.Key.To == node).Sum(e => e.Value);
            if (degree < LooseDegree) loose.Add(node);
            else clustered.Add(node);
        }

        var module = ConsensusModules(clustered, undirected, loose);
        AttachOmnipresent(omni, edges, module, loose);
        return new ConsensusResult(module, loose);
    }

    private static SortedSet<string> Omnipresent(IReadOnlyDictionary<string, string> home,
        IReadOnlyList<(string From, string To, double Cw)> edges)
    {
        var homes = new SortedDictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var (from, to, _) in edges)
        {
            if (!homes.TryGetValue(to, out var set)) homes[to] = set = [];
            set.Add(home[from]);
        }

        return new SortedSet<string>(homes.Where(p => p.Value.Count >= OmniHomes).Select(p => p.Key),
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Undirected weights w(a,b) = cw(a->b) + cw(b->a) with a before b ordinally; omni classes left out.
    /// </summary>
    private static SortedDictionary<EdgeKey, double> Undirected(
        IReadOnlyList<(string From, string To, double Cw)> edges, ISet<string> omni)
    {
        var result = new SortedDictionary<EdgeKey, double>();
        foreach (var (from, to, cw) in edges)
        {
            if (from == to || omni.Contains(from) || omni.Contains(to)) continue;
            var key = string.CompareOrdinal(from, to) < 0 ? new EdgeKey(from, to) : new EdgeKey(to, from);
            result[key] = result.GetValueOrDefault(key) + cw;
        }

        return result;
    }

    private static SortedDictionary<string, int> ConsensusModules(List<string> clustered,
        SortedDictionary<EdgeKey, double> undirected, SortedSet<string> loose)
    {
        var index = clustered.Select((n, i) => (n, i)).ToDictionary(p => p.n, p => p.i, StringComparer.Ordinal);
        var graphEdges = undirected.Where(e => index.ContainsKey(e.Key.From) && index.ContainsKey(e.Key.To))
            .Select(e => (index[e.Key.From], index[e.Key.To], e.Value)).ToList();
        var together = new SortedDictionary<(int A, int B), int>();
        for (var seed = 0; seed < Runs; seed++)
        {
            foreach (var community in LouvainClustering.Communities(clustered.Count, graphEdges, RunResolution, seed))
            {
                for (var x = 0; x < community.Count; x++)
                {
                    for (var y = x + 1; y < community.Count; y++)
                    {
                        var key = (community[x], community[y]);
                        together[key] = together.GetValueOrDefault(key) + 1;
                    }
                }
            }
        }

        var consensusEdges = together.Where(p => p.Value * 2 >= Runs)
            .Select(p => (p.Key.A, p.Key.B, (double)p.Value / Runs)).ToList();
        var consensus = LouvainClustering.Communities(clustered.Count, consensusEdges, 1.0, 0)
            .OrderByDescending(c => c.Count).ThenBy(c => c[0]).ToList();
        var module = new SortedDictionary<string, int>(StringComparer.Ordinal);
        for (var id = 0; id < consensus.Count; id++)
        {
            foreach (var i in consensus[id])
            {
                if (consensus[id].Count < MinModule) loose.Add(clustered[i]);
                else module[clustered[i]] = id;
            }
        }

        return module;
    }

    /// <summary>
    /// An omnipresent class joins the module it references most (summed cw to non-omni members, ties to the lower id).
    /// ASSUMPTION: one with no such reference goes loose; the Python reference put it in a "shared" module.
    /// </summary>
    private static void AttachOmnipresent(SortedSet<string> omni,
        IReadOnlyList<(string From, string To, double Cw)> edges, SortedDictionary<string, int> module,
        SortedSet<string> loose)
    {
        var members = new SortedDictionary<string, int>(module, StringComparer.Ordinal);
        foreach (var node in omni)
        {
            var weights = new SortedDictionary<int, double>();
            foreach (var (from, to, cw) in edges)
            {
                if (from == node && members.TryGetValue(to, out var target))
                    weights[target] = weights.GetValueOrDefault(target) + cw;
            }

            if (weights.Count == 0)
            {
                loose.Add(node);
                continue;
            }

            var best = weights.First();
            foreach (var pair in weights)
            {
                if (pair.Value > best.Value) best = pair;
            }

            module[node] = best.Key;
        }
    }
}
