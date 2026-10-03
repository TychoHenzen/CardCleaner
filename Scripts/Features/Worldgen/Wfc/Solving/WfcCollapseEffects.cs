using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;
using CardCleaner.Scripts.Features.Worldgen.Wfc.Modifiers;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc;

/// <summary>
/// Updates the trackers that depend on collapsed cells: blob sizes, spatial coherence
/// regions and multi-cell reservations.
/// </summary>
internal sealed class WfcCollapseEffects
{
    private readonly BlobSizeTracker? _blobTracker;
    private readonly SpatialCoherenceConstraint? _spatialCoherence;
    private readonly MultiCellReserver _reserver;

    internal WfcCollapseEffects(
        BlobSizeTracker? blobTracker,
        SpatialCoherenceConstraint? spatialCoherence,
        ITileRegistry? tileRegistry)
    {
        _blobTracker = blobTracker;
        _spatialCoherence = spatialCoherence;
        _reserver = new MultiCellReserver(tileRegistry);
    }

    /// <summary>
    /// Clears blob tracking and, for grid topologies, spatial coherence region tracking.
    /// </summary>
    internal void Reset(IWfcTopology topology)
    {
        _blobTracker?.Clear();

        if (_spatialCoherence != null && topology is WfcGrid grid)
            _spatialCoherence.Reset(grid.Width, grid.Height);
    }

    internal void Apply(IWfcTopology topology, int cellId, string tileId)
    {
        RegisterBlob(topology, cellId, tileId);

        // Spatial coherence and multi-cell reservation are grid-specific.
        if (topology is not WfcGrid grid)
            return;

        var position = grid.CellIdToPosition(cellId);
        _spatialCoherence?.OnTileCollapsed(position, tileId, grid);
        _reserver.Reserve(position, tileId, grid);
    }

    private void RegisterBlob(IWfcTopology topology, int cellId, string tileId)
    {
        if (_blobTracker == null)
            return;

        if (topology is WfcGrid grid)
            _blobTracker.RegisterCollapse(grid.CellIdToPosition(cellId), tileId, grid);
        else
            _blobTracker.RegisterCollapse(cellId, tileId, topology);
    }
}
