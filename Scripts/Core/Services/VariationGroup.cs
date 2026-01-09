using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Represents a tile variant with its selection weight.
/// Weight is used for both density calculation and relative probability within the group.
/// </summary>
public readonly struct VariantWeight
{
    public VariantWeight(string tileId, float weight)
    {
        TileId = tileId;
        Weight = weight;
    }

    /// <summary>
    /// The tile ID for this variant.
    /// </summary>
    public string TileId { get; }

    /// <summary>
    /// The selection weight (probability from TSX).
    /// Used for: (1) density calculation via max weight in group, (2) relative selection probability.
    /// </summary>
    public float Weight { get; }
}

/// <summary>
/// A group of tile variants that share a base name and are selected together.
/// For PerGeneration mode: one variant is selected for the entire map.
/// For PerInstance mode: variants are randomly selected per tile placement.
/// </summary>
public class VariationGroup
{
    private readonly List<VariantWeight> _variants;
    private readonly float _maxWeight;

    public VariationGroup(string baseName, VariationMode mode, IEnumerable<VariantWeight> variants)
    {
        BaseName = baseName;
        Mode = mode;
        _variants = variants.ToList();
        _maxWeight = _variants.Count > 0 ? _variants.Max(v => v.Weight) : 1f;
    }

    /// <summary>
    /// The base name for this group (e.g., "grass" for grass1, grass2, grass3).
    /// </summary>
    public string BaseName { get; }

    /// <summary>
    /// How variants are selected: PerGeneration (one for whole map) or PerInstance (per tile).
    /// </summary>
    public VariationMode Mode { get; }

    /// <summary>
    /// All variants in this group with their weights.
    /// </summary>
    public IReadOnlyList<VariantWeight> Variants => _variants;

    /// <summary>
    /// The maximum weight in this group - used as the group's density value.
    /// </summary>
    public float MaxWeight => _maxWeight;

    /// <summary>
    /// Gets the weight for a specific tile ID.
    /// </summary>
    public float GetWeight(string tileId)
    {
        return _variants.FirstOrDefault(v => v.TileId == tileId).Weight;
    }

    /// <summary>
    /// Gets the normalized weight (0-1) for a tile, relative to max weight in group.
    /// Used for weighted random selection within the group.
    /// </summary>
    public float GetNormalizedWeight(string tileId)
    {
        if (_maxWeight <= 0) return 1f;
        return GetWeight(tileId) / _maxWeight;
    }

    /// <summary>
    /// Checks if this group contains the specified tile.
    /// </summary>
    public bool ContainsTile(string tileId)
    {
        return _variants.Any(v => v.TileId == tileId);
    }
}

/// <summary>
/// Collection of variation groups with O(1) lookup by base name or tile ID.
/// </summary>
public class VariationGroupCollection
{
    private readonly Dictionary<string, VariationGroup> _groupsByBase = new();
    private readonly Dictionary<string, string> _tileToBase = new();

    /// <summary>
    /// Adds a variation group to the collection.
    /// </summary>
    public void AddGroup(VariationGroup group)
    {
        _groupsByBase[group.BaseName] = group;

        foreach (var variant in group.Variants)
        {
            _tileToBase[variant.TileId] = group.BaseName;
        }
    }

    /// <summary>
    /// Gets a variation group by its base name (e.g., "grass").
    /// </summary>
    public VariationGroup? GetGroupByBaseName(string baseName)
    {
        return _groupsByBase.GetValueOrDefault(baseName);
    }

    /// <summary>
    /// Finds the variation group containing a specific tile ID.
    /// Returns null if the tile is not in any group.
    /// </summary>
    public VariationGroup? FindGroupContaining(string tileId)
    {
        if (_tileToBase.TryGetValue(tileId, out var baseName))
        {
            return _groupsByBase.GetValueOrDefault(baseName);
        }
        return null;
    }

    /// <summary>
    /// Gets all variants for a base name.
    /// </summary>
    public IReadOnlyList<VariantWeight> GetVariantsFor(string baseName)
    {
        var group = GetGroupByBaseName(baseName);
        return group?.Variants ?? System.Array.Empty<VariantWeight>();
    }

    /// <summary>
    /// Returns all variation groups.
    /// </summary>
    public IEnumerable<VariationGroup> GetAllGroups()
    {
        return _groupsByBase.Values;
    }

    /// <summary>
    /// Clears all groups from the collection.
    /// </summary>
    public void Clear()
    {
        _groupsByBase.Clear();
        _tileToBase.Clear();
    }

    /// <summary>
    /// Number of groups in the collection.
    /// </summary>
    public int Count => _groupsByBase.Count;
}
