using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Wfc.Constraints;

/// <summary>
/// Mesh constraint that prevents different auto-tile types from being adjacent.
/// Auto-tiles of the SAME type can be adjacent (allowing terrain continuity),
/// but different auto-tile types must be separated by gap tiles (non-auto-tiles).
/// This ensures proper bitmask rendering without edge conflicts.
/// </summary>
public class MeshAutoTileGapConstraint : IMeshWfcConstraint
{
    private readonly ITileRegistry _tileRegistry;

    public MeshAutoTileGapConstraint(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public float GetWeightModifier(string tileId, int vertexId, MeshWfcGrid grid)
    {
        var candidateTile = _tileRegistry.GetTile(tileId);
        if (candidateTile == null)
            return 1.0f;

        // If candidate is not an auto-tile, it can go anywhere (gap tiles are flexible)
        if (!candidateTile.HasAutoTileVariants)
            return 1.0f;

        // Candidate IS an auto-tile - check all neighbors
        // No different auto-tile type should be adjacent
        foreach (var neighborId in grid.GetNeighbors(vertexId))
        {
            var neighborCell = grid.GetCell(neighborId);

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
            if (!_tileRegistry.AreSameTerrainType(tileId, neighborTileId))
            {
                return 0.0f; // Hard ban - different auto-tile types cannot be adjacent
            }
        }

        return 1.0f; // Valid placement
    }

    public void OnTileCollapsed(int vertexId, string tileId, MeshWfcGrid grid)
    {
        // No state to update
    }

    public void Reset()
    {
        // No state to reset
    }
}
