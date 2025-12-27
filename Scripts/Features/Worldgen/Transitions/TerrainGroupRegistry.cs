using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Transitions;

/// <summary>
/// Registry of all terrain groups with reverse lookup from tile ID to group.
/// </summary>
public class TerrainGroupRegistry
{
    private readonly Dictionary<string, TerrainGroup> _groups = new();
    private readonly Dictionary<string, string> _tileToGroupId = new();

    public int Count => _groups.Count;

    /// <summary>
    /// Register a terrain group. Updates reverse lookup for all member tiles.
    /// </summary>
    public void RegisterGroup(TerrainGroup group)
    {
        _groups[group.Id] = group;

        foreach (var tileId in group.Members)
            _tileToGroupId[tileId] = group.Id;
    }

    /// <summary>
    /// Get a terrain group by its ID
    /// </summary>
    public TerrainGroup? GetGroup(string groupId) =>
        _groups.GetValueOrDefault(groupId);

    /// <summary>
    /// Get the terrain group that contains a specific tile ID
    /// </summary>
    public TerrainGroup? GetGroupForTile(string tileId)
    {
        if (_tileToGroupId.TryGetValue(tileId, out var groupId))
            return _groups.GetValueOrDefault(groupId);
        return null;
    }

    /// <summary>
    /// Get the group ID for a tile, or null if not in any group
    /// </summary>
    public string? GetGroupIdForTile(string tileId) =>
        _tileToGroupId.GetValueOrDefault(tileId);

    /// <summary>
    /// Check if two tiles are in the same terrain group
    /// </summary>
    public bool AreInSameGroup(string tileId1, string tileId2)
    {
        var group1 = GetGroupIdForTile(tileId1);
        var group2 = GetGroupIdForTile(tileId2);
        return group1 != null && group1 == group2;
    }

    /// <summary>
    /// Get all registered terrain groups
    /// </summary>
    public IEnumerable<TerrainGroup> GetAllGroups() => _groups.Values;

    /// <summary>
    /// Clear all registered groups
    /// </summary>
    public void Clear()
    {
        _groups.Clear();
        _tileToGroupId.Clear();
    }
}
