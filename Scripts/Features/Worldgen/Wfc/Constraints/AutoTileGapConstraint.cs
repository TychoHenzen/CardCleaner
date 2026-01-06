using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Enforces 1-tile gaps between different auto-tile terrain types.
/// Auto-tiles cannot be adjacent (including diagonally) to different auto-tiles;
/// they must have a gap tile (non-auto-tile) between them.
///
/// Uses 8-neighbor check (cardinal + diagonal) because dual-grid rendering
/// samples 4 data corners for each visual tile. Diagonal adjacency would
/// place two different auto-tiles in the same 2x2 visual window.
///
/// This eliminates bitmask conflicts in dual-grid rendering by ensuring
/// any visual tile (which samples 4 data corners) sees at most ONE
/// auto-tile type. The other corners will be gap tiles.
///
/// Rules:
/// - Auto-tile A adjacent (8-way) to same Auto-tile A: ALLOWED (region growth)
/// - Auto-tile A adjacent (8-way) to different Auto-tile B: BANNED
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

        // Candidate IS an auto-tile - check all 8 neighbors (including diagonals)
        // This ensures no 2x2 visual window contains more than one auto-tile type

        // Use precomputed neighbor info if available (optimization)
        if (context.NeighborInfo.HasValue)
        {
            foreach (var kvp in context.NeighborInfo.Value.Neighbors8)
            {
                var neighborTileId = kvp.Value;
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
            return 1.0f;
        }

        // Fallback: iterate neighbors directly (for backward compatibility)
        foreach (var neighborPos in context.Grid.GetNeighbors8(context.Position))
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
