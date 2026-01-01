namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Encourages compact blob shapes by adjusting weights based on same-type neighbor count.
/// Penalizes "snake" extensions (1 neighbor) and rewards "fill" extensions (3-4 neighbors).
///
/// Neighbor count effects:
/// - 0 neighbors: Handled by NoveltySoftModifier, this returns 1.0x
/// - 1 neighbor: Snake/thin extension - apply penalty (default 0.3x)
/// - 2 neighbors: Corner or line continuation - neutral (1.0x)
/// - 3-4 neighbors: Compact fill - apply boost (default 1.5x)
///
/// This shapes blobs toward rounder, more natural-looking formations
/// rather than long thin snakes or tendrils.
/// </summary>
public class CompactnessSoftModifier : ISoftModifier
{
    /// <summary>
    /// Penalty multiplier for snake-like extensions (1 same-type neighbor).
    /// Default 0.3 means snake extensions are 70% less likely.
    /// </summary>
    public float SnakePenalty { get; set; } = 0.3f;

    /// <summary>
    /// Boost multiplier for compact fills (3-4 same-type neighbors).
    /// Default 1.5 means filling in gaps is 50% more likely.
    /// </summary>
    public float CompactBoost { get; set; } = 1.5f;

    public float CalculateMultiplier(SoftModifierContext context)
    {
        var sameTypeNeighborCount = CountSameTypeNeighbors(context);

        return sameTypeNeighborCount switch
        {
            0 => 1.0f,           // No neighbors - handled by NoveltySoftModifier
            1 => SnakePenalty,   // Snake extension - penalize
            2 => 1.0f,           // Corner/line - neutral
            _ => CompactBoost    // 3-4 neighbors - filling in - boost
        };
    }

    private static int CountSameTypeNeighbors(SoftModifierContext context)
    {
        var count = 0;

        foreach (var neighborPos in context.Grid.GetNeighbors(context.Position))
        {
            var neighborCell = context.Grid.GetCell(neighborPos);
            if (neighborCell.IsCollapsed() && neighborCell.GetCollapsedTile() == context.TileId)
            {
                count++;
            }
        }

        return count;
    }
}
