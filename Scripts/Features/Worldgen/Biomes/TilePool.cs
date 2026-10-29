using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

public sealed record TilePool
{
    private TilePool(List<TilePoolEntry> entries)
    {
        Entries = entries.AsReadOnly();
        var total = 0f;
        foreach (var entry in entries)
            total += entry.Weight;
        TotalWeight = total;
    }

    /// <summary>The shared pool with no entries.</summary>
    public static TilePool Empty { get; } = new(new List<TilePoolEntry>());

    // Record equality compares Entries by reference, as the former class did.
    public IReadOnlyList<TilePoolEntry> Entries { get; }

    public bool IsEmpty => Entries.Count == 0;

    public int Count => Entries.Count;

    /// <summary>Sum of the entry weights, summed in <see cref="Entries"/> order at construction.</summary>
    public float TotalWeight { get; }

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
