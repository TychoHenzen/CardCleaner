using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>Prevents auto-tile placements from creating disallowed bitmask patterns.</summary>
public class BitmaskValidityConstraint : IWfcConstraint
{
    private readonly ITileRegistry _tileRegistry;

    public BitmaskValidityConstraint(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        // This constraint is grid-specific (uses 2D positions for bitmask calculation)
        // For non-grid topologies, return neutral (bitmask validation not supported)
        if (context.Topology is not WfcGrid grid)
            return 1.0f;

        var candidateTile = _tileRegistry.GetTile(context.TileId);
        if (candidateTile == null)
            return 1.0f;

        // Only check auto-tile placements
        if (!candidateTile.HasAutoTileVariants)
            return 1.0f;

        var format = candidateTile.GetAutoTileFormat();
        if (format == null)
            return 1.0f;

        var position = grid.CellIdToPosition(context.CellId);

        // For each of the 4 visual tiles affected by this data cell placement,
        // check if the resulting bitmask would be allowed
        var dx = position.X;
        var dy = position.Y;

        // Visual tile at (dx, dy) - candidate is SE corner
        if (!CheckVisualTileBitmask(context, grid, format, dx, dy, CornerRole.SE))
            return 0.0f;

        // Visual tile at (dx+1, dy) - candidate is SW corner
        if (!CheckVisualTileBitmask(context, grid, format, dx + 1, dy, CornerRole.SW))
            return 0.0f;

        // Visual tile at (dx, dy+1) - candidate is NE corner
        if (!CheckVisualTileBitmask(context, grid, format, dx, dy + 1, CornerRole.NE))
            return 0.0f;

        // Visual tile at (dx+1, dy+1) - candidate is NW corner
        if (!CheckVisualTileBitmask(context, grid, format, dx + 1, dy + 1, CornerRole.NW))
            return 0.0f;

        return 1.0f;
    }

    private enum CornerRole { NW, NE, SW, SE }

    /// <summary>
    /// Checks if a visual tile's bitmask would be valid given the candidate placement.
    /// Only validates when all 4 corners are known (collapsed or candidate).
    /// </summary>
    private bool CheckVisualTileBitmask(
        WfcConstraintContext context,
        WfcGrid grid,
        AutoTileFormatDefinition format,
        int visualX,
        int visualY,
        CornerRole candidateCorner)
    {
        // Visual tile at (vx, vy) samples data cells NW (vx-1, vy-1), NE (vx, vy-1),
        // SW (vx-1, vy) and SE (vx, vy). Each corner is filled, a gap, or unknown (null).
        var nwPos = new Vector2I(visualX - 1, visualY - 1);
        var nePos = new Vector2I(visualX, visualY - 1);
        var swPos = new Vector2I(visualX - 1, visualY);
        var sePos = new Vector2I(visualX, visualY);

        var nwFilled = GetCornerState(context, grid, nwPos, candidateCorner == CornerRole.NW);
        var neFilled = GetCornerState(context, grid, nePos, candidateCorner == CornerRole.NE);
        var swFilled = GetCornerState(context, grid, swPos, candidateCorner == CornerRole.SW);
        var seFilled = GetCornerState(context, grid, sePos, candidateCorner == CornerRole.SE);

        // If any corner is unknown (uncollapsed and not the candidate), we cannot validate
        if (nwFilled is not bool nw || neFilled is not bool ne || swFilled is not bool sw || seFilled is not bool se)
            return true; // Can't determine - allow for now

        return format.IsBitmaskAllowed(ToBitmask(nw, ne, sw, se));
    }

    /// <summary>
    /// Computes the Corner16 bitmask: NE=1, SE=2, SW=4, NW=8.
    /// </summary>
    private static int ToBitmask(bool nwFilled, bool neFilled, bool swFilled, bool seFilled)
    {
        var bitmask = 0;
        if (neFilled) bitmask |= 1;
        if (seFilled) bitmask |= 2;
        if (swFilled) bitmask |= 4;
        if (nwFilled) bitmask |= 8;
        return bitmask;
    }

    /// <summary>
    /// Gets the filled state of a corner position.
    /// </summary>
    /// <returns>
    /// true if the corner is filled with the candidate's auto-tile type,
    /// false if it's a gap/different tile,
    /// null if it's uncollapsed and not the candidate position.
    /// </returns>
    private bool? GetCornerState(WfcConstraintContext context, WfcGrid grid, Vector2I pos, bool isCandidate)
    {
        // If this is the candidate position, it's filled with the auto-tile
        if (isCandidate)
            return true;

        // Check if position is out of bounds (treat as gap)
        if (pos.X < 0 || pos.Y < 0 || pos.X >= grid.Width || pos.Y >= grid.Height)
            return false;

        // Get the cell at this position
        var cell = grid.GetCell(pos);

        // If not collapsed, we don't know the state
        if (!cell.IsCollapsed())
            return null;

        // Check if the collapsed tile is the same auto-tile type
        var collapsedTileId = cell.GetCollapsedTile();
        if (collapsedTileId == context.TileId)
            return true;

        // Check if it's ANY auto-tile of the same format
        // (Different auto-tile types shouldn't be adjacent per gap constraint,
        // but we check for same-type here)
        var collapsedTile = _tileRegistry.GetTile(collapsedTileId);
        if (collapsedTile != null && collapsedTile.HasAutoTileVariants)
        {
            // It's an auto-tile, but different type - treat as filled
            // (Gap constraint should prevent this, but handle it anyway)
            return true;
        }

        // It's a gap tile or non-auto-tile
        return false;
    }
}
