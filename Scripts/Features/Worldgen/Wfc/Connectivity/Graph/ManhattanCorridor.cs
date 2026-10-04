using System;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Wfc.Connectivity;

/// <summary>
/// Geometry for the L-shaped Manhattan corridors between two points.
/// </summary>
internal static class ManhattanCorridor
{
    /// <summary>
    /// Checks if a position is on or near the L-shaped Manhattan path between two points.
    /// The path goes horizontal first, then vertical (or the reverse).
    /// </summary>
    internal static bool IsOnPath(Vector2I pos, Vector2I from, Vector2I to, int tolerance)
    {
        // Path 1: horizontal then vertical
        // From (from.X, from.Y) to (to.X, from.Y) to (to.X, to.Y)
        if (IsNearLineSegment(pos, from, new Vector2I(to.X, from.Y), tolerance) ||
            IsNearLineSegment(pos, new Vector2I(to.X, from.Y), to, tolerance))
            return true;

        // Path 2: vertical then horizontal
        // From (from.X, from.Y) to (from.X, to.Y) to (to.X, to.Y)
        if (IsNearLineSegment(pos, from, new Vector2I(from.X, to.Y), tolerance) ||
            IsNearLineSegment(pos, new Vector2I(from.X, to.Y), to, tolerance))
            return true;

        return false;
    }

    /// <summary>
    /// Checks if a position is within tolerance of an axis-aligned line segment.
    /// </summary>
    private static bool IsNearLineSegment(Vector2I pos, Vector2I a, Vector2I b, int tolerance)
    {
        // Horizontal segment
        if (a.Y == b.Y)
        {
            var minX = Math.Min(a.X, b.X);
            var maxX = Math.Max(a.X, b.X);
            return pos.X >= minX - tolerance && pos.X <= maxX + tolerance &&
                   Math.Abs(pos.Y - a.Y) <= tolerance;
        }

        // Vertical segment
        if (a.X == b.X)
        {
            var minY = Math.Min(a.Y, b.Y);
            var maxY = Math.Max(a.Y, b.Y);
            return pos.Y >= minY - tolerance && pos.Y <= maxY + tolerance &&
                   Math.Abs(pos.X - a.X) <= tolerance;
        }

        return false;
    }
}
