using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Encourages compact blob shapes by boosting tiles that fill in gaps between existing tiles.
/// Does NOT penalize normal edge growth (1 neighbor) - only rewards compact fills.
///
/// Neighbor count effects:
/// - 0 neighbors: No match - neutral (1.0x)
/// - 1 neighbor: Normal edge extension - neutral (1.0x)
/// - 2 neighbors: Corner fill - small boost (default 1.3x)
/// - 3-4 neighbors: Gap fill - larger boost (default 2.0x)
///
/// This shapes blobs toward rounder, more natural-looking formations
/// without preventing normal region growth from edges.
/// </summary>
public class CompactnessSoftModifier : IWfcConstraint
{
    /// <summary>
    /// Boost multiplier for corner fills (2 same-type neighbors).
    /// Default 1.3 means corner fills are 30% more likely.
    /// </summary>
    public float CornerBoost { get; set; } = 1.3f;

    /// <summary>
    /// Boost multiplier for gap fills (3-4 same-type neighbors).
    /// Default 2.0 means filling in gaps is 100% more likely.
    /// </summary>
    public float GapFillBoost { get; set; } = 2.0f;

    /// <inheritdoc />
    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // Use precomputed neighbor info if available (optimization)
        var sameTypeNeighborCount = context.NeighborInfo.HasValue
            ? context.NeighborInfo.Value.SameType4Count
            : CountSameTypeNeighbors(context);

        return sameTypeNeighborCount switch
        {
            0 => 1.0f,           // No match - neutral
            1 => 1.0f,           // Normal edge extension - neutral (don't penalize growth!)
            2 => CornerBoost,    // Corner fill - small boost
            _ => GapFillBoost    // 3-4 neighbors - gap fill - larger boost
        };
    }

    private static int CountSameTypeNeighbors(WfcConstraintContext context)
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
