using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services;

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
