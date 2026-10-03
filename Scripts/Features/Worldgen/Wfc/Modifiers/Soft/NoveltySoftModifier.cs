using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers.Soft;

/// <summary>Boosts tiles that would start a new blob without same-type neighbors.</summary>
public class NoveltySoftModifier : IWfcConstraint
{
    /// <summary>Sets the weight multiplier for isolated tile placements.</summary>
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
            ? context.NeighborInfo.Value.SameTypeCount
            : CountSameTypeNeighbors(context);

        // Only boost tiles that would start a new isolated blob
        return sameTypeNeighborCount == 0 ? NoveltyBoost : 1.0f;
    }

    private static int CountSameTypeNeighbors(WfcConstraintContext context)
    {
        var count = 0;

        // Use topology-agnostic neighbor iteration
        foreach (var neighborId in context.Topology.GetNeighbors(context.CellId))
        {
            var neighborCell = context.Topology.GetCell(neighborId);
            if (neighborCell.IsCollapsed() && neighborCell.GetCollapsedTile() == context.TileId)
            {
                count++;
            }
        }

        return count;
    }
}
