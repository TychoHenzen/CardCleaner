using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// The layered module order (#240 requirement 7): Data, Contracts, middle, UI, never interleaved. Inside a layer the
/// strongly connected components are placed in topological order, foundation first, each in the exact order with the
/// fewest up edges and then the least up weight. A component above ExactLimit modules is ordered greedily.
/// </summary>
internal static class ModuleOrder
{
    public const int ExactLimit = 20;

    public static ModuleOrderResult Order(IReadOnlyDictionary<string, Layer> moduleLayer,
        IReadOnlyDictionary<EdgeKey, ModuleEdge> edges)
    {
        var order = new List<string>();
        var greedy = new List<List<string>>();
        foreach (var layer in Enum.GetValues<Layer>())
        {
            var modules = moduleLayer.Where(p => p.Value == layer).Select(p => p.Key)
                .OrderBy(m => m, StringComparer.Ordinal).ToList();
            var set = modules.ToHashSet(StringComparer.Ordinal);
            var inner = edges.Where(e => set.Contains(e.Key.From) && set.Contains(e.Key.To))
                .ToDictionary(e => e.Key, e => e.Value.Weight);
            foreach (var component in Topological(StronglyConnected.Components(modules, inner.Keys), inner.Keys))
            {
                if (component.Count > ExactLimit)
                {
                    greedy.Add(component);
                    order.AddRange(Greedy(component, inner));
                }
                else
                {
                    order.AddRange(Exact(component, inner));
                }
            }
        }

        var position = order.Select((m, i) => (m, i)).ToDictionary(p => p.m, p => p.i, StringComparer.Ordinal);
        var up = edges.Where(e => position[e.Key.From] < position[e.Key.To])
            .OrderByDescending(e => e.Value.Weight).ThenBy(e => e.Key).Select(e => e.Key).ToList();
        var loops = StronglyConnected.Components(moduleLayer.Keys, edges.Keys).Where(c => c.Count > 1)
            .OrderByDescending(c => c.Count).ThenBy(c => c[0], StringComparer.Ordinal).ToList();
        return new ModuleOrderResult(order, up, loops, greedy);
    }

    /// <summary>Components foundation first: one is placed once every component it references is placed.</summary>
    private static List<List<string>> Topological(List<List<string>> components, IEnumerable<EdgeKey> edges)
    {
        var owner = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var c = 0; c < components.Count; c++)
        {
            foreach (var m in components[c]) owner[m] = c;
        }

        var needs = components.Select(_ => new HashSet<int>()).ToList();
        foreach (var edge in edges)
        {
            if (owner[edge.From] != owner[edge.To]) needs[owner[edge.From]].Add(owner[edge.To]);
        }

        var placed = new HashSet<int>();
        var result = new List<List<string>>();
        while (result.Count < components.Count)
        {
            var next = Enumerable.Range(0, components.Count)
                .Where(c => !placed.Contains(c) && needs[c].All(placed.Contains))
                .OrderBy(c => components[c][0], StringComparer.Ordinal).First();
            placed.Add(next);
            result.Add(components[next]);
        }

        return result;
    }

    /// <summary>Subset DP: best[T] = min over v in T of best[T \ v] plus the up edges into v from T \ v.</summary>
    private static List<string> Exact(List<string> members, Dictionary<EdgeKey, long> edges)
    {
        var n = members.Count;
        var (predecessors, weight) = Matrix(members, edges);
        var full = (1 << n) - 1;
        var cost = new (int Count, long Weight)[full + 1];
        var last = new int[full + 1];
        for (var set = 1; set <= full; set++)
        {
            cost[set] = (int.MaxValue, long.MaxValue);
            for (var v = 0; v < n; v++)
            {
                if ((set & (1 << v)) == 0) continue;
                var before = set & ~(1 << v);
                var ups = before & predecessors[v];
                var candidate = (cost[before].Count + BitOperations.PopCount((uint)ups),
                    cost[before].Weight + UpWeight(ups, v, weight));
                if (candidate.CompareTo(cost[set]) >= 0) continue;
                cost[set] = candidate;
                last[set] = v;
            }
        }

        var order = new List<string>();
        for (var set = full; set != 0; set &= ~(1 << last[set])) order.Add(members[last[set]]);
        order.Reverse();
        return order;
    }

    /// <summary>predecessors[v] has bit u set when u references v; weight[u, v] is that edge's weight.</summary>
    private static (int[] Predecessors, long[,] Weight) Matrix(List<string> members, Dictionary<EdgeKey, long> edges)
    {
        var n = members.Count;
        var predecessors = new int[n];
        var weight = new long[n, n];
        for (var u = 0; u < n; u++)
        {
            for (var v = 0; v < n; v++)
            {
                if (u == v || !edges.TryGetValue(new EdgeKey(members[u], members[v]), out var w)) continue;
                predecessors[v] |= 1 << u;
                weight[u, v] = w;
            }
        }

        return (predecessors, weight);
    }

    private static long UpWeight(int ups, int v, long[,] weight)
    {
        var total = 0L;
        for (var u = 0; ups >> u != 0; u++)
        {
            if ((ups & (1 << u)) != 0) total += weight[u, v];
        }

        return total;
    }

    /// <summary>
    /// Repeatedly place the module with the fewest edges to unplaced modules (they become up), ties ordinal.
    /// </summary>
    private static List<string> Greedy(List<string> members, Dictionary<EdgeKey, long> edges)
    {
        var remaining = new SortedSet<string>(members, StringComparer.Ordinal);
        var order = new List<string>();
        while (remaining.Count > 0)
        {
            var next = remaining.OrderBy(m => remaining.Count(o => edges.ContainsKey(new EdgeKey(m, o))))
                .ThenBy(m => m, StringComparer.Ordinal).First();
            remaining.Remove(next);
            order.Add(next);
        }

        return order;
    }
}
