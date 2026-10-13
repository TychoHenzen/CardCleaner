using System;

namespace CardCleaner.Tests.Architecture.DependencyGate;

/// <summary>A directed class pair, ordered ordinally by From and then To.</summary>
internal readonly record struct EdgeKey(string From, string To) : IComparable<EdgeKey>
{
    public int CompareTo(EdgeKey other)
    {
        var byFrom = string.CompareOrdinal(From, other.From);
        return byFrom != 0 ? byFrom : string.CompareOrdinal(To, other.To);
    }
}
