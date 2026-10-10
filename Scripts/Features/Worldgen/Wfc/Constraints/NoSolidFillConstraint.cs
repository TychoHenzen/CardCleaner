using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Prevents 2x2 solid regions for auto-tiles that lack a bitmask 15 (solid fill) variant.
///
/// In dual-grid auto-tiling, a 2x2 region of the same auto-tile type produces bitmask 15
/// (all 4 corners filled). Some tilesets (e.g., edge-based hedges converted to corner format)
/// don't have this variant, so we must prevent such configurations.
///
/// For each candidate placement, checks all four 2x2 windows the cell could complete.
/// If the other 3 cells are the same auto-tile type AND that type lacks bitmask 15, ban placement.
/// </summary>
public class NoSolidFillConstraint : IWfcConstraint
{
    private readonly IWfcTileCatalog _tileCatalog;

    // The four 2x2 window offsets relative to the candidate cell position.
    // Each window is defined by the 3 OTHER cells that would form a 2x2 with the candidate.
    private static readonly Vector2I[][] WindowOffsets =
    [
        // Window where candidate is SE corner: check NW, N, W
        [new Vector2I(-1, -1), new Vector2I(0, -1), new Vector2I(-1, 0)],
        // Window where candidate is SW corner: check N, NE, E
        [new Vector2I(0, -1), new Vector2I(1, -1), new Vector2I(1, 0)],
        // Window where candidate is NE corner: check W, SW, S
        [new Vector2I(-1, 0), new Vector2I(-1, 1), new Vector2I(0, 1)],
        // Window where candidate is NW corner: check E, S, SE
        [new Vector2I(1, 0), new Vector2I(0, 1), new Vector2I(1, 1)]
    ];

    public NoSolidFillConstraint(IWfcTileCatalog tileCatalog)
    {
        _tileCatalog = tileCatalog;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // This constraint is grid-specific (uses 2D positions for 2x2 window checking)
        // For non-grid topologies, return neutral (solid fill validation not supported)
        if (context.Topology is not WfcGrid grid)
            return 1.0f;

        // Only applies to auto-tiles
        if (!_tileCatalog.IsAutoTile(context.TileId))
            return 1.0f;

        // Check if THIS tile's auto-tile format lacks bitmask 15
        var hasSolidFill = _tileCatalog.HasSolidFillVariant(context.TileId);

        // If this tile has solid fill, no restriction needed
        if (hasSolidFill)
            return 1.0f;

        var position = grid.CellIdToPosition(context.CellId);

        // Check all four 2x2 windows this cell could complete
        foreach (var offsets in WindowOffsets)
        {
            if (WouldCompleteSolidRegion(context, grid, position, offsets))
            {
                return 0.0f; // Ban - would create 2x2 region without solid fill variant
            }
        }

        return 1.0f;
    }

    private bool WouldCompleteSolidRegion(
        WfcConstraintContext context,
        WfcGrid grid,
        Vector2I position,
        Vector2I[] neighborOffsets)
    {
        string? matchingTileId = null;

        foreach (var offset in neighborOffsets)
        {
            // Outside the grid, uncollapsed, or not an auto-tile: can't form a complete same-type 2x2
            var neighborTileId = CollapsedAutoTileAt(grid, position + offset);
            if (neighborTileId == null)
                return false;

            // Check if all neighbors are the same terrain type
            if (matchingTileId == null)
                matchingTileId = neighborTileId;
            else if (!_tileCatalog.AreSameTerrainType(matchingTileId, neighborTileId))
                return false; // Different terrain types - not a solid region
        }

        // All 3 neighbors are the same auto-tile type
        // Check if candidate is also the same type
        return matchingTileId != null &&
               _tileCatalog.AreSameTerrainType(context.TileId, matchingTileId);
    }

    /// <summary>
    /// Returns the collapsed tile at a position when it is an auto-tile, otherwise null.
    /// </summary>
    private string? CollapsedAutoTileAt(WfcGrid grid, Vector2I pos)
    {
        if (!grid.IsInBounds(pos))
            return null;

        var cell = grid.GetCell(pos);
        if (!cell.IsCollapsed())
            return null;

        var tileId = cell.GetCollapsedTile();
        return _tileCatalog.IsAutoTile(tileId) ? tileId : null;
    }
}
