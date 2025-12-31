using System.Collections.Generic;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Worldgen.WeightModifiers;

/// <summary>
/// Configuration resource defining tile affinity values for a specific biome.
/// Affinities multiply tile weights during selection - values > 1.0 increase likelihood,
/// values < 1.0 decrease likelihood. Unmapped tiles default to 1.0 (neutral).
/// </summary>
[Tool]
[GlobalClass]
public partial class BiomeTileAffinity : Resource
{
    private const string DefaultBiome = "plains";
    private System.Collections.Generic.Dictionary<string, float>? _affinityCache;
    private bool _cacheInvalid = true;

    public BiomeTileAffinity() { }

    public BiomeTileAffinity(string biomeId, params TileAffinityEntry[] entries)
    {
        Biome = biomeId;
        foreach (var entry in entries)
            Entries.Add(entry);
    }

    /// <summary>The biome these affinities apply to.</summary>
    [Export] public string Biome { get; set; } = DefaultBiome;

    /// <summary>List of tile affinity entries.</summary>
    [Export] public Array<TileAffinityEntry> Entries { get; set; } = [];

    /// <summary>
    /// Get the affinity multiplier for a given tile.
    /// Returns 1.0 (neutral) for unmapped tiles.
    /// </summary>
    public float GetAffinity(string tileId)
    {
        RebuildCacheIfNeeded();
        return _affinityCache!.GetValueOrDefault(tileId, 1.0f);
    }

    /// <summary>
    /// Get all affinities as a dictionary for batch processing.
    /// </summary>
    public IReadOnlyDictionary<string, float> GetAllAffinities()
    {
        RebuildCacheIfNeeded();
        return _affinityCache!;
    }

    /// <summary>Mark the cache as invalid (call after modifying Entries).</summary>
    public void MarkDirty() => _cacheInvalid = true;

    private void RebuildCacheIfNeeded()
    {
        if (!_cacheInvalid && _affinityCache != null)
            return;

        _affinityCache = new System.Collections.Generic.Dictionary<string, float>(Entries.Count);
        foreach (var entry in Entries)
        {
            if (!string.IsNullOrEmpty(entry.TileId))
                _affinityCache[entry.TileId] = entry.Affinity;
        }
        _cacheInvalid = false;
    }

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Biome) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Biome) => DefaultBiome,
            _ => base._PropertyGetRevert(property)
        };
    }
}
