using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

[Tool]
[GlobalClass]
public partial class TilePoolEntry : Resource
{
    public TilePoolEntry() { }

    public TilePoolEntry(string tileId, float weight = 1.0f)
    {
        TileId = tileId;
        Weight = weight;
    }

    [Export] public string TileId { get; set; } = string.Empty;
    [Export] public float Weight { get; set; } = 1.0f;
}

[Tool]
[GlobalClass]
public partial class TilePool : Resource
{
    private float _totalWeight;
    private bool _weightsDirty = true;

    [Export] public Array<TilePoolEntry> Entries { get; set; } = new();

    public bool IsEmpty => Entries.Count == 0;

    public int Count => Entries.Count;

    public void Add(string tileId, float weight = 1.0f)
    {
        Entries.Add(new TilePoolEntry(tileId, weight));
        _weightsDirty = true;
    }

    public string? SelectRandom(RandomNumberGenerator rng)
    {
        if (Entries.Count == 0)
            return null;

        RecalculateWeightsIfNeeded();

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

    private void RecalculateWeightsIfNeeded()
    {
        if (!_weightsDirty)
            return;

        _totalWeight = 0f;
        foreach (var entry in Entries)
            _totalWeight += entry.Weight;

        _weightsDirty = true;
    }

    public void MarkDirty() => _weightsDirty = true;
}
