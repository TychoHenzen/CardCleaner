using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class SimpleVisibilityChecker : IVisibilityChecker
{
    public bool CanSee(Vector2I from, Vector2I target, SimpleMapData mapData)
    {
        if (from == target) return true;

        // Bresenham's line algorithm with diagonal corner blocking
        var x0 = from.X;
        var y0 = from.Y;
        var x1 = target.X;
        var y1 = target.Y;

        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            // Check if we've reached the target
            if (x0 == x1 && y0 == y1)
                return true;

            var currentPos = new Vector2I(x0, y0);

            // Check if this tile blocks visibility (skip the starting tile)
            if (currentPos != from && !mapData.IsTransparent(currentPos))
                return false;

            var e2 = 2 * err;
            var movingX = e2 > -dy;
            var movingY = e2 < dx;

            // Check for diagonal corner blocking:
            // If moving diagonally and both adjacent tiles are opaque, block visibility
            if (movingX && movingY)
            {
                var horizontalNeighbor = new Vector2I(x0 + sx, y0);
                var verticalNeighbor = new Vector2I(x0, y0 + sy);

                // If both tiles adjacent to the diagonal are opaque, we can't see through the corner
                if (!mapData.IsTransparent(horizontalNeighbor) && !mapData.IsTransparent(verticalNeighbor))
                    return false;
            }

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
}
