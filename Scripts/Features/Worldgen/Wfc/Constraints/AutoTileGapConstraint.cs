using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Enforces 1-tile gaps between different auto-tile terrain types.
/// Auto-tiles cannot be directly adjacent to different auto-tiles;
/// they must have a gap tile (non-auto-tile) between them.
///
/// This eliminates bitmask conflicts in dual-grid rendering by ensuring
/// any visual tile (which samples 4 data corners) sees at most ONE
/// auto-tile type. The other corners will be gap tiles.
///
/// Rules:
/// - Auto-tile A adjacent to same Auto-tile A: ALLOWED (region growth)
/// - Auto-tile A adjacent to different Auto-tile B: BANNED
/// - Gap tile adjacent to any tile: ALLOWED
/// </summary>
public class AutoTileGapConstraint : IWfcConstraint
{
    private readonly ITileRegistry _tileRegistry;

    public AutoTileGapConstraint(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        var candidateTile = _tileRegistry.GetTile(context.TileId);
        if (candidateTile == null)
            return 1.0f;

        // If candidate is not an auto-tile, it can go anywhere
        if (!candidateTile.HasAutoTileVariants)
            return 1.0f;

        // Candidate IS an auto-tile - check all 4 cardinal neighbors
        foreach (var neighborPos in context.Grid.GetNeighbors(context.Position))
        {
            var neighborCell = context.Grid.GetCell(neighborPos);

            // Only check collapsed neighbors
            if (!neighborCell.IsCollapsed())
                continue;

            var neighborTileId = neighborCell.GetCollapsedTile();
            var neighborTile = _tileRegistry.GetTile(neighborTileId);
            if (neighborTile == null)
                continue;

            // If neighbor is not an auto-tile, no problem
            if (!neighborTile.HasAutoTileVariants)
                continue;

            // Both are auto-tiles - must be SAME type
            if (context.TileId != neighborTileId)
            {
                return 0.0f; // Hard ban - different auto-tiles cannot be adjacent
            }
        }

        return 1.0f; // Valid placement
    }
}
