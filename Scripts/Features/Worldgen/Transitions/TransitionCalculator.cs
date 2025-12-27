using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Transitions;

/// <summary>
/// Calculates 4-bit transition bitmasks for each tile position.
/// Bitmask bits: N=1, E=2, S=4, W=8 (where bit is set if neighbor is in a different terrain group)
/// </summary>
public class TransitionCalculator
{
    // Direction offsets: North, East, South, West
    private static readonly Vector2I[] Directions =
    [
        new(0, -1),  // North (bit 0 = 1)
        new(1, 0),   // East (bit 1 = 2)
        new(0, 1),   // South (bit 2 = 4)
        new(-1, 0)   // West (bit 3 = 8)
    ];

    private readonly TerrainGroupRegistry _groupRegistry;
    private readonly TransitionRuleRegistry _ruleRegistry;

    public TransitionCalculator(TerrainGroupRegistry groupRegistry, TransitionRuleRegistry ruleRegistry)
    {
        _groupRegistry = groupRegistry;
        _ruleRegistry = ruleRegistry;
    }

    /// <summary>
    /// Calculate transition bitmask for a tile position.
    /// Each bit indicates if the neighbor in that direction is in a different terrain group.
    /// </summary>
    /// <param name="position">The tile position to calculate for</param>
    /// <param name="tileIds">The 2D array of tile IDs</param>
    /// <param name="mapSize">The map dimensions</param>
    /// <returns>4-bit bitmask (0-15). 0 means no transitions needed.</returns>
    public int CalculateBitmask(Vector2I position, string[,] tileIds, Vector2I mapSize)
    {
        var myTileId = tileIds[position.Y, position.X];
        var myGroup = _groupRegistry.GetGroupForTile(myTileId);

        // Tiles not in any group don't get transitions
        if (myGroup == null)
            return 0;

        var mask = 0;
        for (var i = 0; i < 4; i++)
        {
            var neighborPos = position + Directions[i];

            // Out of bounds counts as different (edge of map)
            if (!IsInBounds(neighborPos, mapSize))
            {
                mask |= 1 << i;
                continue;
            }

            var neighborTile = tileIds[neighborPos.Y, neighborPos.X];
            var neighborGroup = _groupRegistry.GetGroupForTile(neighborTile);

            // Different group (or ungrouped neighbor) = set the bit
            if (neighborGroup == null || neighborGroup.Id != myGroup.Id)
                mask |= 1 << i;
        }

        return mask;
    }

    /// <summary>
    /// Get the transition tile ID to render for a position.
    /// Returns null if no transition is needed or no rule exists.
    /// </summary>
    /// <param name="position">The tile position</param>
    /// <param name="tileIds">The 2D array of tile IDs</param>
    /// <param name="mapSize">The map dimensions</param>
    /// <returns>Transition tile ID, or null if none needed</returns>
    public string? GetTransitionTileId(Vector2I position, string[,] tileIds, Vector2I mapSize)
    {
        var myTileId = tileIds[position.Y, position.X];
        var myGroup = _groupRegistry.GetGroupForTile(myTileId);

        if (myGroup == null)
            return null;

        var bitmask = CalculateBitmask(position, tileIds, mapSize);

        // No transitions needed (all neighbors are same group)
        if (bitmask == 0)
            return null;

        // Find the dominant neighbor group to determine which transition rule to use
        var neighborGroup = GetDominantNeighborGroup(position, tileIds, mapSize, myGroup.Id);
        if (neighborGroup == null)
            return null;

        // Get the transition rule for this pair
        var rule = _ruleRegistry.GetRuleByPriority(myGroup, neighborGroup);
        if (rule == null)
            return null;

        // Get the transition tile for this bitmask
        return rule.GetTransitionTile(bitmask);
    }

    /// <summary>
    /// Find the most common neighboring terrain group that differs from the current tile.
    /// </summary>
    private TerrainGroup? GetDominantNeighborGroup(Vector2I position, string[,] tileIds,
        Vector2I mapSize, string excludeGroupId)
    {
        TerrainGroup? dominant = null;
        var highestPriority = int.MinValue;

        foreach (var direction in Directions)
        {
            var neighborPos = position + direction;
            if (!IsInBounds(neighborPos, mapSize))
                continue;

            var neighborTile = tileIds[neighborPos.Y, neighborPos.X];
            var neighborGroup = _groupRegistry.GetGroupForTile(neighborTile);

            if (neighborGroup == null || neighborGroup.Id == excludeGroupId)
                continue;

            // Use priority to determine dominance
            if (neighborGroup.Priority > highestPriority)
            {
                highestPriority = neighborGroup.Priority;
                dominant = neighborGroup;
            }
        }

        return dominant;
    }

    private static bool IsInBounds(Vector2I pos, Vector2I mapSize)
    {
        return pos.X >= 0 && pos.X < mapSize.X && pos.Y >= 0 && pos.Y < mapSize.Y;
    }
}
