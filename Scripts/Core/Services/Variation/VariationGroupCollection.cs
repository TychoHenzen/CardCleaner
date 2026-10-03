using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Services;

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
