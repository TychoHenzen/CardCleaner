using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>
/// Every reference from one class to another, as reference sites per kind. Both weights derive from the merged
/// counts, so sites added in several calls weigh the same as one call with their sum (#240 requirement 2).
/// </summary>
internal sealed class EdgeWeight
{
    private readonly SortedDictionary<EdgeKind, int> _sites = new();

    public void Add(EdgeKind kind, int count) => _sites[kind] = _sites.GetValueOrDefault(kind) + count;

    /// <summary>Sum of kind weight x count: the module-level weight.</summary>
    public long Weight => _sites.Sum(p => (long)ClassGraph.KindWeight(p.Key) * p.Value);

    /// <summary>Sum of kind weight x (1 + log2 count): the clustering weight.</summary>
    public double ClusterWeight => _sites.Sum(p => ClassGraph.KindWeight(p.Key) * (1 + Math.Log2(p.Value)));
}
