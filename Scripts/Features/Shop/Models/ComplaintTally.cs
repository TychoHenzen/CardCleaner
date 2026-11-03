using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Counts the customer complaints of one shop day by reason.</summary>
public sealed class ComplaintTally
{
    private readonly Dictionary<string, int> _counts = new(StringComparer.Ordinal);

    /// <summary>Records one complaint. A blank reason is ignored.</summary>
    public void Add(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return;

        _counts[reason] = _counts.GetValueOrDefault(reason) + 1;
    }

    /// <summary>The complaints grouped by reason, most frequent first, ties broken by reason.</summary>
    public IReadOnlyList<(string Reason, int Count)> Groups() =>
        _counts
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => (pair.Key, pair.Value))
            .ToList();

    /// <summary>Forgets every complaint.</summary>
    public void Clear() => _counts.Clear();
}
