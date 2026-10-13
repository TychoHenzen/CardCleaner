using System;
using System.Collections.Generic;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>Lifts class edges to module edges; references inside one module and classes without a module are skipped.</summary>
internal static class ModuleEdges
{
    public static SortedDictionary<EdgeKey, ModuleEdge> Build(ClassGraph graph, Func<string, string?> moduleOf)
    {
        var result = new SortedDictionary<EdgeKey, ModuleEdge>();
        foreach (var (key, weight) in graph.Edges)
        {
            var from = moduleOf(key.From);
            var to = moduleOf(key.To);
            if (from is null || to is null || from == to) continue;
            var moduleKey = new EdgeKey(from, to);
            if (!result.TryGetValue(moduleKey, out var edge)) result[moduleKey] = edge = new ModuleEdge();
            edge.Weight += weight.Weight;
            edge.Pairs.Add((key.From, key.To, weight.Weight));
        }

        foreach (var edge in result.Values)
        {
            edge.Pairs.Sort((x, y) => y.Weight != x.Weight ? y.Weight.CompareTo(x.Weight)
                : new EdgeKey(x.From, x.To).CompareTo(new EdgeKey(y.From, y.To)));
        }

        return result;
    }
}
