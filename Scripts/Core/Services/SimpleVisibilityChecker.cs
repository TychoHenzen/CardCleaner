using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public class SimpleVisibilityChecker : IVisibilityChecker
{
    public bool CanSee(Vector2I from, Vector2I to, SimpleMapData mapData)
    {
        if (from == to) return true;

        // Bresenham's line algorithm
        var x0 = from.X;
        var y0 = from.Y;
        var x1 = to.X;
        var y1 = to.Y;

        var dx = Math.Abs(x1 - x0);
        var dy = Math.Abs(y1 - y0);
        var sx = x0 < x1 ? 1 : -1;
        var sy = y0 < y1 ? 1 : -1;
        var err = dx - dy;

        while (true)
        {
            var currentPos = new Vector2I(x0, y0);

            // Check if we've reached the target
            if (x0 == x1 && y0 == y1)
                return true;

            // Check if this tile blocks visibility (skip the starting tile)
            if (currentPos != from && !mapData.IsTransparent(currentPos))
                return false;

            var e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }
}
