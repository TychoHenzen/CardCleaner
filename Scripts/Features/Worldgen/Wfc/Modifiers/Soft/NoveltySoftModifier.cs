using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>
/// Boosts tiles that would start a new blob (no same-type neighbors).
/// Compensates for the lack of continuity bias on isolated tile placements,
/// allowing new terrain types to establish themselves against dominant blobs.
///
/// Without this modifier, tiles with no same-type neighbors get:
/// - No continuity boost (1.0x)
/// - Diminishing returns penalty (~0.667x for potential size 1)
/// Net: ~0.667x base weight
///
/// Meanwhile, dominant tiles get:
/// - Continuity boost (5.0x)
/// - Diminishing returns penalty (varies with blob size)
/// At blob size 10: 5.0 * 0.167 = 0.835x - still competitive!
///
/// This modifier gives a configurable boost (default 3.0x) to "new blob starters",
/// making them competitive with mid-sized dominant blobs.
/// </summary>
public class NoveltySoftModifier : IWfcConstraint
{
    /// <summary>
    /// Multiplier boost for tiles with no same-type neighbors.
    /// Default 3.0 means new blobs get 3x weight, helping them compete
    /// with continuity-boosted dominant tiles.
    /// </summary>
    public float NoveltyBoost { get; set; } = 3.0f;

    /// <summary>
    /// Creates a novelty modifier with default settings.
    /// </summary>
    public NoveltySoftModifier()
    {
    }

    /// <inheritdoc />
    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // Use precomputed neighbor info if available (optimization)
        var sameTypeNeighborCount = context.NeighborInfo.HasValue
            ? context.NeighborInfo.Value.SameType4Count
            : CountSameTypeNeighbors(context);

        // Only boost tiles that would start a new isolated blob
        return sameTypeNeighborCount == 0 ? NoveltyBoost : 1.0f;
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
