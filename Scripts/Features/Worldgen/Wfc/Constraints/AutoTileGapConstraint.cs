namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>Enforces one-tile gaps between different auto-tile terrain types.</summary>
public class AutoTileGapConstraint : IWfcConstraint
{
    private readonly IWfcTileCatalog _tileCatalog;

    public AutoTileGapConstraint(IWfcTileCatalog tileCatalog)
    {
        _tileCatalog = tileCatalog;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // If candidate is not an auto-tile, it can go anywhere
        if (!_tileCatalog.IsAutoTile(context.TileId))
            return 1.0f;

        // Candidate IS an auto-tile - check all neighbors
        // (8-way for rect grid, quad-sharing for irregular mesh)
        // Hard ban when a different auto-tile type would be adjacent.
        var hasConflict = context.NeighborInfo.HasValue
            ? HasConflictInNeighborInfo(context)
            : HasConflictInTopology(context);

        return hasConflict ? 0.0f : 1.0f;
    }

    // Uses precomputed neighbor info (optimization)
    private bool HasConflictInNeighborInfo(WfcConstraintContext context)
    {
        foreach (var kvp in context.NeighborInfo!.Value.WindowNeighbors)
        {
            if (ConflictsWithNeighbor(_tileCatalog, context.TileId, kvp.Value))
                return true;
        }

        return false;
    }

    // Fallback: iterate neighbors directly from topology
    private bool HasConflictInTopology(WfcConstraintContext context)
    {
        foreach (var neighborId in context.Topology.GetWindowNeighbors(context.CellId))
        {
            var neighborCell = context.Topology.GetCell(neighborId);

            // Only check collapsed neighbors
            if (!neighborCell.IsCollapsed())
                continue;

            if (ConflictsWithNeighbor(_tileCatalog, context.TileId, neighborCell.GetCollapsedTile()))
                return true;
        }

        return false;
    }

    // Two auto-tiles may only touch when they are the SAME terrain type (allowing variations).
    private static bool ConflictsWithNeighbor(
        IWfcTileCatalog tileCatalog,
        string candidateTileId,
        string neighborTileId)
    {
        if (!tileCatalog.IsAutoTile(neighborTileId))
            return false;

        return !tileCatalog.AreSameTerrainType(candidateTileId, neighborTileId);
    }
}
