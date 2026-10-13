using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Seam ratings between real modules (#240 requirement 8): defined when no concrete class is crossed in either
/// direction; missing when the top three targets carry under 60% of the weight and at least four concrete classes
/// are crossed; otherwise leaky.
/// </summary>
internal static class SeamRater
{
    private const double MissingShare = 0.6;
    private const int MissingConcrete = 4;

    public static List<SeamRating> Rate(IReadOnlyDictionary<EdgeKey, ModuleEdge> edges,
        IReadOnlyDictionary<string, GateNode> nodes)
    {
        var ratings = new List<SeamRating>();
        foreach (var (key, edge) in edges.OrderBy(e => e.Key))
        {
            var into = new SortedDictionary<string, long>(StringComparer.Ordinal);
            foreach (var (_, to, weight) in edge.Pairs) into[to] = into.GetValueOrDefault(to) + weight;
            var ranked = into.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal).ToList();
            var total = ranked.Sum(p => p.Value);
            var topThree = total == 0 ? 1.0 : (double)ranked.Take(3).Sum(p => p.Value) / total;
            var concrete = ranked.Where(p => !nodes[p.Key].IsContractRole).Select(p => p.Key).ToList();
            var concreteBack = edges.TryGetValue(new EdgeKey(key.To, key.From), out var back) &&
                               back.Pairs.Any(p => !nodes[p.To].IsContractRole);
            var rating = concrete.Count == 0 && !concreteBack ? SeamKind.Defined
                : topThree < MissingShare && concrete.Count >= MissingConcrete ? SeamKind.Missing
                : SeamKind.Leaky;
            ratings.Add(new SeamRating(key, rating, topThree, concrete, concreteBack));
        }

        return ratings;
    }
}
