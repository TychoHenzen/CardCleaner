using CardCleaner.Scripts.Core.Interfaces;
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
    private const int SolidFillBitmask = 15;

    private readonly ITileRegistry _tileRegistry;

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

    public NoSolidFillConstraint(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // This constraint is grid-specific (uses 2D positions for 2x2 window checking)
        // For non-grid topologies, return neutral (solid fill validation not supported)
        if (context.Topology is not WfcGrid grid)
            return 1.0f;

        var candidateTile = _tileRegistry.GetTile(context.TileId);
        if (candidateTile == null)
            return 1.0f;

        // Only applies to auto-tiles
        if (!candidateTile.HasAutoTileVariants)
            return 1.0f;

        // Check if THIS tile's auto-tile format lacks bitmask 15
        var hasSolidFill = HasSolidFillVariant(candidateTile);

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

    private bool WouldCompleteSolidRegion(WfcConstraintContext context, WfcGrid grid, Vector2I position, Vector2I[] neighborOffsets)
    {
        string? matchingTileId = null;

        foreach (var offset in neighborOffsets)
        {
            var neighborPos = position + offset;

            // Bounds check - if any neighbor is outside the grid, can't form a complete 2x2
            if (neighborPos.X < 0 || neighborPos.Y < 0 ||
                neighborPos.X >= grid.Width || neighborPos.Y >= grid.Height)
                return false;

            var neighborCell = grid.GetCell(neighborPos);

            // If neighbor isn't collapsed yet, can't form a complete 2x2
            if (!neighborCell.IsCollapsed())
                return false;

            var neighborTileId = neighborCell.GetCollapsedTile();
            var neighborTile = _tileRegistry.GetTile(neighborTileId);

            // If neighbor isn't an auto-tile, can't form a same-type 2x2
            if (neighborTile == null || !neighborTile.HasAutoTileVariants)
                return false;

            // Check if all neighbors are the same terrain type
            if (matchingTileId == null)
            {
                matchingTileId = neighborTileId;
            }
            else if (!_tileRegistry.AreSameTerrainType(matchingTileId, neighborTileId))
            {
                return false; // Different terrain types - not a solid region
            }
        }

        // All 3 neighbors are the same auto-tile type
        // Check if candidate is also the same type
        return matchingTileId != null &&
               _tileRegistry.AreSameTerrainType(context.TileId, matchingTileId);
    }

    private static bool HasSolidFillVariant(Features.Deckbuilder.Tiles.TileDefinition tile)
    {
        var variants = tile.AutoTileVariants;
        if (variants == null || variants.Length <= SolidFillBitmask)
            return false;

        return variants[SolidFillBitmask].HasValue;
    }
}
