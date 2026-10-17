using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// The tile pairs that may touch, as plain data. The composition root builds it once from the compiled transition
/// map, so the map generators take this value and never name the resolver that produced it.
/// </summary>
public sealed class TransitionPairs
{
    public TransitionPairs(IEnumerable<(string tileA, string tileB)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);
        Pairs = pairs.ToList();
    }

    /// <summary>The pairs in the order they were given.</summary>
    public IReadOnlyList<(string tileA, string tileB)> Pairs { get; }
}
