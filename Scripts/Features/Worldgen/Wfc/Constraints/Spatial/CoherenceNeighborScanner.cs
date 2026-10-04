namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Measures how strongly a candidate tile extends existing regions around a cell.
/// </summary>
internal static class CoherenceNeighborScanner
{
    internal static CoherenceNeighborScan Scan(WfcConstraintContext context, RegionTracker? regionTracker)
    {
        // Spatial coherence grows regions through face neighbors. WFC topology adjacency
        // remains separate because it also covers cells sharing 2x2 windows.
        if (context.Topology is WfcGrid grid)
            return ScanGrid(context, grid, regionTracker!);

        // Use precomputed neighbor info if available (optimization) for non-grid topologies.
        if (context.NeighborInfo.HasValue)
            return FromNeighborInfo(context.NeighborInfo.Value);

        return ScanTopology(context);
    }

    private static CoherenceNeighborScan ScanGrid(
        WfcConstraintContext context,
        WfcGrid grid,
        RegionTracker regionTracker)
    {
        var largestMatchingRegion = 0;
        var hasAnyCollapsedNeighbor = false;
        var position = grid.CellIdToPosition(context.CellId);

        foreach (var neighbor in grid.GetNeighbors(position))
        {
            var neighborTile = grid.GetCollapsedTileAt(neighbor);
            if (neighborTile == null)
                continue;

            hasAnyCollapsedNeighbor = true;
            if (neighborTile != context.TileId)
                continue;

            var regionSize = regionTracker.GetRegionSize(neighbor);
            if (regionSize > largestMatchingRegion)
                largestMatchingRegion = regionSize;
        }

        return new CoherenceNeighborScan(largestMatchingRegion, hasAnyCollapsedNeighbor);
    }

    // Non-grid topology: the same-type count stands in for the region size (a mesh simplification).
    private static CoherenceNeighborScan FromNeighborInfo(CollapsedNeighborInfo neighborInfo)
    {
        var largestMatchingRegion = neighborInfo.SameTypeCount > 0 ? neighborInfo.SameTypeCount : 0;
        return new CoherenceNeighborScan(largestMatchingRegion, neighborInfo.HasCollapsedNeighbor);
    }

    private static CoherenceNeighborScan ScanTopology(WfcConstraintContext context)
    {
        var hasAnyCollapsedNeighbor = false;
        var sameTypeNeighborCount = 0;

        foreach (var neighborId in context.Topology.GetNeighbors(context.CellId))
        {
            var neighborTile = context.Topology.GetCollapsedTileAt(neighborId);
            if (neighborTile == null)
                continue;

            hasAnyCollapsedNeighbor = true;
            if (neighborTile == context.TileId)
                sameTypeNeighborCount++;
        }

        return new CoherenceNeighborScan(sameTypeNeighborCount, hasAnyCollapsedNeighbor);
    }
}
