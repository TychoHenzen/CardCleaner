using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The rules for moving furniture on the floor: grid snapping, quarter turns, the footprint of a pose and
///     whether a pose is valid. Pure rules, no engine types.
/// </summary>
public static class FurniturePlacementModel
{
    /// <summary>The grid furniture snaps to, in metres.</summary>
    public const float GridSize = 0.5f;

    /// <summary>Snaps a coordinate to the nearest grid line, rounding halves away from zero.</summary>
    public static float Snap(float value) =>
        (float)Math.Round(value / GridSize, MidpointRounding.AwayFromZero) * GridSize;

    /// <summary>Adds a number of quarter turns and wraps the result into 0 to 3.</summary>
    public static int Turn(int quarterTurns, int delta) => (((quarterTurns + delta) % 4) + 4) % 4;

    /// <summary>
    ///     The floor rectangle a piece of furniture covers at a pose. Odd quarter turns swap width and depth.
    /// </summary>
    public static FurnitureRect RectOf(FurnitureFootprint footprint, FurniturePose pose)
    {
        var swapped = pose.QuarterTurns % 2 != 0;
        var halfX = (swapped ? footprint.Depth : footprint.Width) / 2f;
        var halfZ = (swapped ? footprint.Width : footprint.Depth) / 2f;
        return new FurnitureRect(pose.X - halfX, pose.Z - halfZ, pose.X + halfX, pose.Z + halfZ);
    }

    /// <summary>
    ///     True when the footprint at the pose lies inside the floor and overlaps none of the placed rectangles.
    /// </summary>
    public static bool IsValid(
        FurnitureFootprint footprint,
        FurniturePose pose,
        IReadOnlyList<FurnitureRect> placed,
        FurnitureRect floor)
    {
        var rect = RectOf(footprint, pose);
        if (!floor.Contains(rect))
            return false;
        foreach (var other in placed)
        {
            if (rect.Overlaps(other))
                return false;
        }

        return true;
    }
}
