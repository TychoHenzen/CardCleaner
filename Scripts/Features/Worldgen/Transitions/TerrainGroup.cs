using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Transitions;

/// <summary>
/// Defines a group of tiles that share transition behavior.
/// Tiles in the same group don't need edge transitions between them.
/// Higher priority groups render their edges on top of lower priority groups.
/// </summary>
public class TerrainGroup
{
    private readonly HashSet<string> _members;

    public TerrainGroup(string id, string name, int priority, IEnumerable<string> members)
    {
        Id = id;
        Name = name;
        Priority = priority;
        _members = new HashSet<string>(members);
    }

    /// <summary>
    /// Unique identifier for this terrain group (e.g., "grass", "dirt", "sand")
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Display name for editor/debug purposes
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Priority for edge rendering. Higher priority groups render their edges
    /// on top of lower priority groups at transitions.
    /// </summary>
    public int Priority { get; }

    /// <summary>
    /// Set of tile IDs that belong to this group
    /// </summary>
    public IReadOnlySet<string> Members => _members;

    /// <summary>
    /// Check if a tile ID belongs to this terrain group
    /// </summary>
    public bool Contains(string tileId) => _members.Contains(tileId);
}
