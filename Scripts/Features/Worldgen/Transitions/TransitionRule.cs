using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Worldgen.Transitions;

/// <summary>
/// Defines transition tiles for edges between two terrain groups.
/// Maps 4-bit bitmask (0-15) to edge overlay tile IDs.
/// Bitmask bits: N=1, E=2, S=4, W=8
/// </summary>
public class TransitionRule
{
    private readonly Dictionary<int, string?> _edgeTiles;

    public TransitionRule(string fromGroupId, string toGroupId, Dictionary<int, string?> edgeTiles)
    {
        FromGroupId = fromGroupId;
        ToGroupId = toGroupId;
        _edgeTiles = new Dictionary<int, string?>(edgeTiles);
    }

    /// <summary>
    /// The terrain group that has the edges (higher priority, renders on top)
    /// </summary>
    public string FromGroupId { get; }

    /// <summary>
    /// The terrain group being transitioned to (lower priority, underneath)
    /// </summary>
    public string ToGroupId { get; }

    /// <summary>
    /// Get the transition tile ID for a specific bitmask.
    /// Returns null if no transition tile is defined for this bitmask.
    /// </summary>
    /// <param name="bitmask">4-bit edge bitmask (0-15). Bits: N=1, E=2, S=4, W=8</param>
    public string? GetTransitionTile(int bitmask) =>
        _edgeTiles.GetValueOrDefault(bitmask);

    /// <summary>
    /// Check if this rule has a transition tile for the given bitmask
    /// </summary>
    public bool HasTransitionTile(int bitmask) =>
        _edgeTiles.TryGetValue(bitmask, out var tile) && tile != null;
}
