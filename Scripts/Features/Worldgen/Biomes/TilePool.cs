using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public sealed record TilePool
{
    private readonly float _totalWeight;

    private TilePool(List<TilePoolEntry> entries)
    {
        Entries = entries.AsReadOnly();
        var total = 0f;
        foreach (var entry in entries)
            total += entry.Weight;
        _totalWeight = total;
    }

    /// <summary>The shared pool with no entries.</summary>
    public static TilePool Empty { get; } = new(new List<TilePoolEntry>());

    // Record equality compares Entries by reference, as the former class did.
    public IReadOnlyList<TilePoolEntry> Entries { get; }

    public bool IsEmpty => Entries.Count == 0;

    public int Count => Entries.Count;

    public string? SelectRandom(IRandom rng)
    {
        if (Entries.Count == 0)
            return null;

        if (_totalWeight <= 0)
            return Entries[0].TileId;

        var roll = rng.Randf() * _totalWeight;
        var cumulative = 0f;

        foreach (var entry in Entries)
        {
            cumulative += entry.Weight;
            if (roll <= cumulative)
                return entry.TileId;
        }

        return Entries[Entries.Count - 1].TileId;
    }

    /// <summary>Get all tile IDs in this pool.</summary>
    public IEnumerable<string> GetAllTileIds()
    {
        foreach (var entry in Entries)
        {
            if (!string.IsNullOrEmpty(entry.TileId))
                yield return entry.TileId;
        }
    }

    /// <summary>Collects entries in insertion order and builds the immutable pool.</summary>
    public sealed class Builder
    {
        private readonly List<TilePoolEntry> _entries = [];

        public Builder Add(string tileId, float weight = 1.0f)
        {
            _entries.Add(new TilePoolEntry(tileId, weight));
            return this;
        }

        public TilePool Build() => new(new List<TilePoolEntry>(_entries));
    }
}
