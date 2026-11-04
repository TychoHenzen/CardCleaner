using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     An axis-aligned rectangle on the shop floor, in metres. X runs along the floor's width and Z along its depth.
///     Pure rules, no engine types.
/// </summary>
public readonly record struct FurnitureRect(float MinX, float MinZ, float MaxX, float MaxZ)
{
    /// <summary>Tolerance for float comparisons, in metres.</summary>
    public const float Epsilon = 0.0001f;

    /// <summary>True only when the two rectangles share area larger than <see cref="Epsilon"/> on both axes.</summary>
    public bool Overlaps(FurnitureRect other) =>
        Math.Min(MaxX, other.MaxX) - Math.Max(MinX, other.MinX) > Epsilon
        && Math.Min(MaxZ, other.MaxZ) - Math.Max(MinZ, other.MinZ) > Epsilon;

    /// <summary>True when <paramref name="inner"/> lies inside this rectangle on all four sides.</summary>
    public bool Contains(FurnitureRect inner) =>
        inner.MinX >= MinX - Epsilon
        && inner.MaxX <= MaxX + Epsilon
        && inner.MinZ >= MinZ - Epsilon
        && inner.MaxZ <= MaxZ + Epsilon;
}
