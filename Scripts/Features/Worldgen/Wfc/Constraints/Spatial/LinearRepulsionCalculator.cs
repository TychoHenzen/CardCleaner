using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Constraints;

/// <summary>
/// Calculates the repulsion penalty for linear tiles (hedges, paths) from nearby same-type tiles.
/// This creates sparse, spread-out structures instead of dense clusters.
/// </summary>
internal sealed class LinearRepulsionCalculator
{
    private readonly ITileRegistry _tileRegistry;

    internal LinearRepulsionCalculator(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    internal float Calculate(WfcConstraintContext context, LinearRepulsionSettings settings)
    {
        // Only works for grid topologies (requires coordinate-based distance)
        if (context.Topology is not WfcGrid grid)
            return 1.0f;

        var totalPenalty = 0.0f;
        var pos = grid.CellIdToPosition(context.CellId);

        // Scan within repulsion radius
        for (var dy = -settings.Radius; dy <= settings.Radius; dy++)
        {
            for (var dx = -settings.Radius; dx <= settings.Radius; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                totalPenalty += PenaltyAt(context, grid, new Vector2I(pos.X + dx, pos.Y + dy), settings, dx, dy);
            }
        }

        // Convert accumulated penalty to modifier (clamped to MinModifier)
        return Mathf.Max(settings.MinModifier, 1.0f - totalPenalty);
    }

    private float PenaltyAt(
        WfcConstraintContext context,
        WfcGrid grid,
        Vector2I checkPos,
        LinearRepulsionSettings settings,
        int dx,
        int dy)
    {
        if (!grid.IsInBounds(checkPos))
            return 0.0f;

        var cell = grid.GetCell(checkPos);
        if (!cell.IsCollapsed())
            return 0.0f;

        // Same tile type (using terrain type comparison for auto-tiles)
        if (!_tileRegistry.AreSameTerrainType(context.TileId, cell.GetCollapsedTile()))
            return 0.0f;

        // Chebyshev distance for grid. Penalty diminishes linearly with distance:
        // at distance 1 full penalty, at radius zero penalty.
        var distance = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
        var distanceFactor = 1.0f - (float)(distance - 1) / settings.Radius;
        return settings.Strength * distanceFactor;
    }
}
