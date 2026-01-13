using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Unified constraint that prevents different auto-tile types from being adjacent.
/// Works with any IWfcGrid implementation (regular grids or irregular meshes).
///
/// For regular grids: Uses 8-way adjacency check to ensure no 2x2 visual window
/// contains more than one auto-tile type (important for dual-grid rendering).
///
/// For irregular meshes: Uses the grid's natural neighbor connectivity.
/// </summary>
public class UnifiedAutoTileGapConstraint : IUnifiedWfcConstraint
{
    private readonly ITileRegistry _tileRegistry;

    public UnifiedAutoTileGapConstraint(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public float GetWeightModifier(int cellId, string tileId, IWfcGrid grid)
    {
        var candidateTile = _tileRegistry.GetTile(tileId);
        if (candidateTile == null)
            return 1.0f;

        // If candidate is not an auto-tile, it can go anywhere (gap tiles are flexible)
        if (!candidateTile.HasAutoTileVariants)
            return 1.0f;

        // Candidate IS an auto-tile - check neighbors
        // Use 8-way for regular grids (for dual-grid 2x2 window constraint)
        // Use natural neighbors for irregular meshes
        var neighbors = grid is IWfcGrid8Way grid8Way
            ? grid8Way.GetNeighbors8(cellId)
            : grid.GetNeighbors(cellId);

        foreach (var neighborId in neighbors)
        {
            var neighborTile = grid.GetCollapsedTileAt(neighborId);
            if (neighborTile == null)
                continue;

            var neighborTileDef = _tileRegistry.GetTile(neighborTile);
            if (neighborTileDef == null)
                continue;

            // If neighbor is not an auto-tile, no problem
            if (!neighborTileDef.HasAutoTileVariants)
                continue;

            // Both are auto-tiles - must be SAME terrain type (allowing variations)
            if (!_tileRegistry.AreSameTerrainType(tileId, neighborTile))
            {
                return 0.0f; // Hard ban - different auto-tile types cannot be adjacent
            }
        }

        return 1.0f; // Valid placement
    }

    public void OnTileCollapsed(int cellId, string tileId, IWfcGrid grid)
    {
        // No state to update
    }

    public void Reset()
    {
        // No state to reset
    }
}
