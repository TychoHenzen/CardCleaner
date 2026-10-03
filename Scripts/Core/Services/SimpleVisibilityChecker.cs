using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Visibility checker using Bresenham's line algorithm.
/// Works with any IMapData but optimized for regular grids.
/// </summary>
public class SimpleVisibilityChecker : IVisibilityChecker
{
    /// <summary>
    /// Number of steps per cell for world position line tracing.
    /// </summary>
    public int StepsPerCell { get; set; } = 2;

    public bool CanSee(int fromCellId, int toCellId, IMapData mapData)
    {
        if (fromCellId == toCellId) return true;

        // Use world positions for line tracing
        var from = mapData.GetCellCenter(fromCellId);
        var to = mapData.GetCellCenter(toCellId);
        return CanSee(from, to, mapData);
    }

    public bool CanSee(Vector2 from, Vector2 to, IMapData mapData)
    {
        if (from.IsEqualApprox(to)) return true;

        // Check if both positions are in valid cells
        var fromCell = mapData.GetCellAtPosition(from);
        var toCell = mapData.GetCellAtPosition(to);

        if (!fromCell.HasValue || !toCell.HasValue)
            return false;

        if (fromCell.Value == toCell.Value)
            return true;

        // For RegularGridMapData, use optimized Bresenham approach
        if (mapData is RegularGridMapData regularGrid)
        {
            return CanSeeBresenham(fromCell.Value, toCell.Value, regularGrid);
        }

        // For irregular grids, use line sampling approach
        return CanSeeSampled(from, to, fromCell.Value, mapData);
    }

    /// <summary>
    /// Optimized visibility check for regular grids using Bresenham's algorithm.
    /// </summary>
    private bool CanSeeBresenham(int fromCellId, int toCellId, RegularGridMapData gridData)
    {
        var from = gridData.GetGridPosition(fromCellId);
        var to = gridData.GetGridPosition(toCellId);

        var x0 = from.X;
        var y0 = from.Y;
        var x1 = to.X;
        var y1 = to.Y;

        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = StepToward(x0, x1);
        var sy = StepToward(y0, y1);
        var err = dx - dy;

        while (true)
        {
            // Check if we've reached the target
            if (x0 == x1 && y0 == y1)
                return true;

            // Check if this tile blocks visibility (skip the starting tile)
            if (BlocksVisibility(gridData, new Vector2I(x0, y0), fromCellId))
                return false;

            var e2 = 2 * err;
            var movingX = e2 > -dy;
            var movingY = e2 < dx;

            // If moving diagonally and both adjacent tiles are opaque, block visibility
            if (movingX && movingY && IsDiagonalCornerBlocked(
                    gridData, new Vector2I(x0 + sx, y0), new Vector2I(x0, y0 + sy)))
                return false;

            if (movingX)
            {
                err -= dy;
                x0 += sx;
            }
            if (movingY)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    private static int StepToward(int from, int to) => from < to ? 1 : -1;

    private static bool BlocksVisibility(RegularGridMapData gridData, Vector2I position, int fromCellId)
    {
        var cellId = gridData.GetCellId(position);
        return cellId != fromCellId && !gridData.IsTransparent(cellId);
    }

    private static bool IsDiagonalCornerBlocked(
        RegularGridMapData gridData, Vector2I horizontalPos, Vector2I verticalPos)
    {
        var hCellId = gridData.GetCellId(horizontalPos);
        var vCellId = gridData.GetCellId(verticalPos);

        // If both tiles adjacent to the diagonal are opaque, we can't see through the corner
        return !gridData.IsTransparent(hCellId) && !gridData.IsTransparent(vCellId);
    }

    /// <summary>
    /// Visibility check for irregular grids using line sampling.
    /// Traces points along the line and checks cell transparency.
    /// </summary>
    private bool CanSeeSampled(Vector2 from, Vector2 to, int fromCellId, IMapData mapData)
    {
        var distance = from.DistanceTo(to);

        // Estimate cell size from area
        var fromArea = mapData.GetCellArea(fromCellId);
        var cellSize = Mathf.Sqrt(fromArea);

        // Calculate number of sample points
        var numSteps = Mathf.Max(2, Mathf.CeilToInt(distance / cellSize) * StepsPerCell);
        var stepVector = (to - from) / numSteps;

        int? lastCellId = fromCellId;

        for (var i = 1; i < numSteps; i++)
        {
            var samplePoint = from + stepVector * i;
            var cellId = mapData.GetCellAtPosition(samplePoint);

            if (!cellId.HasValue)
                continue; // Outside map bounds, continue

            // Skip if same as last cell (avoid redundant checks)
            if (cellId.Value == lastCellId)
                continue;

            // Skip the starting cell
            if (cellId.Value == fromCellId)
                continue;

            // Check if this cell blocks visibility
            if (!mapData.IsTransparent(cellId.Value))
                return false;

            lastCellId = cellId.Value;
        }

        return true;
    }
}
