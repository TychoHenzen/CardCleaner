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
///
/// Tiles without bitmask 15 (solid fill) are excluded from compactness boosting,
/// allowing them to form linear/path-like shapes instead of round blobs.
/// </summary>
public class CompactnessSoftModifier : IWfcConstraint
{
    private readonly IWfcTileCatalog? _tileCatalog;

    public CompactnessSoftModifier(IWfcTileCatalog? tileCatalog = null)
    {
        _tileCatalog = tileCatalog;
    }

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
        // Skip compactness boost for tiles without solid fill (e.g., hedges)
        // These should form linear/path-like shapes, not round blobs
        if (_tileCatalog != null && !HasSolidFillVariant(context.TileId))
            return 1.0f;

        // Use precomputed neighbor info if available (optimization)
        var sameTypeNeighborCount = context.NeighborInfo.HasValue
            ? context.NeighborInfo.Value.SameTypeCount
            : CountSameTypeNeighbors(context);

        return sameTypeNeighborCount switch
        {
            0 => 1.0f,           // No match - neutral
            1 => 1.0f,           // Normal edge extension - neutral (don't penalize growth!)
            2 => CornerBoost,    // Corner fill - small boost
            _ => GapFillBoost    // 3-4 neighbors - gap fill - larger boost
        };
    }

    private bool HasSolidFillVariant(string tileId)
    {
        // Unknown tiles count as having solid fill; non-auto-tiles do not
        return _tileCatalog == null || !_tileCatalog.Contains(tileId) || _tileCatalog.HasSolidFillVariant(tileId);
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
