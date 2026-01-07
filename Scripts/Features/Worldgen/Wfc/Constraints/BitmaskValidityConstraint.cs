using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// WFC constraint that ensures auto-tile placements don't create disallowed bitmask patterns.
/// Works with the dual-grid auto-tile system where each visual tile samples 4 data cells.
/// </summary>
/// <remarks>
/// When an auto-tile format has disabled bitmasks (e.g., "walled" format disables 5, 10, 15),
/// this constraint prevents tile configurations that would result in those bitmask patterns.
///
/// For corner4 bitmasks:
/// - NE=1, SE=2, SW=4, NW=8
/// - Bitmask 5 (NE+SW) = diagonal corners only
/// - Bitmask 10 (SE+NW) = opposite diagonal corners
/// - Bitmask 15 = all corners filled (interior)
///
/// A data cell at (dx, dy) affects visual tiles at:
/// - Visual (dx, dy): data cell is SE corner
/// - Visual (dx+1, dy): data cell is SW corner
/// - Visual (dx, dy+1): data cell is NE corner
/// - Visual (dx+1, dy+1): data cell is NW corner
/// </remarks>
public class BitmaskValidityConstraint : IWfcConstraint
{
    private readonly ITileRegistry _tileRegistry;

    public BitmaskValidityConstraint(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public float GetProbabilityModifier(WfcConstraintContext context)
    {
        var candidateTile = _tileRegistry.GetTile(context.TileId);
        if (candidateTile == null)
            return 1.0f;

        // Only check auto-tile placements
        if (!candidateTile.HasAutoTileVariants)
            return 1.0f;

        var format = candidateTile.GetAutoTileFormat();
        if (format == null)
            return 1.0f;

        // For each of the 4 visual tiles affected by this data cell placement,
        // check if the resulting bitmask would be allowed
        var dx = context.Position.X;
        var dy = context.Position.Y;

        // Visual tile at (dx, dy) - candidate is SE corner
        if (!CheckVisualTileBitmask(context, format, dx, dy, CornerRole.SE))
            return 0.0f;

        // Visual tile at (dx+1, dy) - candidate is SW corner
        if (!CheckVisualTileBitmask(context, format, dx + 1, dy, CornerRole.SW))
            return 0.0f;

        // Visual tile at (dx, dy+1) - candidate is NE corner
        if (!CheckVisualTileBitmask(context, format, dx, dy + 1, CornerRole.NE))
            return 0.0f;

        // Visual tile at (dx+1, dy+1) - candidate is NW corner
        if (!CheckVisualTileBitmask(context, format, dx + 1, dy + 1, CornerRole.NW))
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
        AutoTileFormatDefinition format,
        int visualX,
        int visualY,
        CornerRole candidateCorner)
    {
        // Get the 4 data cell positions for this visual tile
        // Visual tile at (vx, vy) samples:
        // - NW: data (vx-1, vy-1)
        // - NE: data (vx, vy-1)
        // - SW: data (vx-1, vy)
        // - SE: data (vx, vy)
        var nwPos = new Vector2I(visualX - 1, visualY - 1);
        var nePos = new Vector2I(visualX, visualY - 1);
        var swPos = new Vector2I(visualX - 1, visualY);
        var sePos = new Vector2I(visualX, visualY);

        // Determine the state of each corner
        bool? nwFilled = GetCornerState(context, nwPos, candidateCorner == CornerRole.NW);
        bool? neFilled = GetCornerState(context, nePos, candidateCorner == CornerRole.NE);
        bool? swFilled = GetCornerState(context, swPos, candidateCorner == CornerRole.SW);
        bool? seFilled = GetCornerState(context, sePos, candidateCorner == CornerRole.SE);

        // If any corner is unknown (uncollapsed and not the candidate), we can't validate
        if (!nwFilled.HasValue || !neFilled.HasValue || !swFilled.HasValue || !seFilled.HasValue)
            return true; // Can't determine - allow for now

        // Compute the bitmask (Corner16 format: NE=1, SE=2, SW=4, NW=8)
        var bitmask = 0;
        if (neFilled.Value) bitmask |= 1;  // NE
        if (seFilled.Value) bitmask |= 2;  // SE
        if (swFilled.Value) bitmask |= 4;  // SW
        if (nwFilled.Value) bitmask |= 8;  // NW

        // Check if this bitmask is allowed by the format
        return format.IsBitmaskAllowed(bitmask);
    }

    /// <summary>
    /// Gets the filled state of a corner position.
    /// </summary>
    /// <returns>
    /// true if the corner is filled with the candidate's auto-tile type,
    /// false if it's a gap/different tile,
    /// null if it's uncollapsed and not the candidate position.
    /// </returns>
    private bool? GetCornerState(WfcConstraintContext context, Vector2I pos, bool isCandidate)
    {
        // If this is the candidate position, it's filled with the auto-tile
        if (isCandidate)
            return true;

        // Check if position is out of bounds (treat as gap)
        if (pos.X < 0 || pos.Y < 0 || pos.X >= context.Grid.Width || pos.Y >= context.Grid.Height)
            return false;

        // Get the cell at this position
        var cell = context.Grid.GetCell(pos);

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
