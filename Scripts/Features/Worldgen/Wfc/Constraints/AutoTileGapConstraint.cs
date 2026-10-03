using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>Enforces one-tile gaps between different auto-tile terrain types.</summary>
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

        // Candidate IS an auto-tile - check all neighbors
        // (8-way for rect grid, quad-sharing for irregular mesh)

        // Use precomputed neighbor info if available (optimization)
        if (context.NeighborInfo.HasValue)
        {
            foreach (var kvp in context.NeighborInfo.Value.Neighbors)
            {
                var neighborTileId = kvp.Value;
                var neighborTile = _tileRegistry.GetTile(neighborTileId);
                if (neighborTile == null)
                    continue;

                // If neighbor is not an auto-tile, no problem
                if (!neighborTile.HasAutoTileVariants)
                    continue;

                // Both are auto-tiles - must be SAME terrain type (allowing variations)
                if (!_tileRegistry.AreSameTerrainType(context.TileId, neighborTileId))
                {
                    return 0.0f; // Hard ban - different auto-tile types cannot be adjacent
                }
            }
            return 1.0f;
        }

        // Fallback: iterate neighbors directly from topology
        foreach (var neighborId in context.Topology.GetNeighbors(context.CellId))
        {
            var neighborCell = context.Topology.GetCell(neighborId);

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

            // Both are auto-tiles - must be SAME terrain type (allowing variations)
            if (!_tileRegistry.AreSameTerrainType(context.TileId, neighborTileId))
            {
                return 0.0f; // Hard ban - different auto-tile types cannot be adjacent
            }
        }

        return 1.0f; // Valid placement
    }
}
