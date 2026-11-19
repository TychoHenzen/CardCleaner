using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     Where one item lies in a box: its shape, its pose, and the cell it fills, given by the cell's low corner and
///     size in the box's inner frame (the floor at y 0, the back left corner at x 0 and z 0). A cell is the posed
///     item plus <see cref="BoxPacker.Clearance" /> on each side across the floor, so neighbours never touch.
/// </summary>
public readonly record struct PackedItem(ItemShape Shape, ItemPose Pose, Vector3 Min, Vector3 Size)
{
    public Vector3 Max => Min + Size;

    public Vector3 Center => Min + Size / 2f;
}

/// <summary>
///     Packs items into a box one at a time without overlap. An item goes where it rests lowest, then furthest back,
///     then furthest left, in the first of its poses (<see cref="ItemShapes.Poses" />) that fits anywhere: on the
///     floor, or on items below it whose tops carry at least <see cref="SupportShare" /> of its base. The spots tried
///     are the box's back left corner and the corners next to, on and behind every item already in. A card or booster
///     goes straight on top of the item put in just before it when that item has the same shape, so a run of cards
///     becomes one stack. The same items in the same order always land in the same places.
/// </summary>
public sealed class BoxPacker
{
    /// <summary>Gap left on each side of an item across the floor, so items side by side never touch.</summary>
    public const float Clearance = 0.002f;

    /// <summary>The share of an item's base that the tops below it must carry when it does not stand on the floor.</summary>
    public const float SupportShare = 0.6f;

    private const float Epsilon = 1e-4f;

    public BoxPacker(Vector3 interior)
    {
        Interior = interior;
    }

    /// <summary>The inner size of the box, which every item must stay within.</summary>
    public Vector3 Interior { get; }

    /// <summary>
    ///     Where an item of <paramref name="shape" /> goes, given the items <paramref name="placed" /> so far; null
    ///     when it fits nowhere.
    /// </summary>
    public PackedItem? Place(IReadOnlyList<PackedItem> placed, ItemShape shape)
    {
        if (OnTopOfLast(placed, shape) is { } stacked)
            return stacked;

        var corners = Corners(placed);
        foreach (var pose in ItemShapes.Poses(shape))
        {
            var size = Cell(shape, pose);
            foreach (var corner in corners)
            {
                if (Fits(placed, corner, size))
                    return new PackedItem(shape, pose, corner, size);
            }
        }

        return null;
    }

    private PackedItem? OnTopOfLast(IReadOnlyList<PackedItem> placed, ItemShape shape)
    {
        if (placed.Count == 0 || !ItemShapes.Stacks(shape) || placed[^1].Shape != shape)
            return null;

        var last = placed[^1];
        var min = new Vector3(last.Min.X, last.Max.Y, last.Min.Z);
        return Fits(placed, min, last.Size) ? last with { Min = min } : null;
    }

    private static Vector3 Cell(ItemShape shape, ItemPose pose) =>
        ItemShapes.SizeIn(ItemShapes.Size(shape), pose) + new Vector3(2f * Clearance, 0f, 2f * Clearance);

    // The spots to try, lowest first, then furthest back (low z), then furthest left (low x).
    private static List<Vector3> Corners(IReadOnlyList<PackedItem> placed)
    {
        var corners = new List<Vector3>(placed.Count * 3 + 1) { Vector3.Zero };
        foreach (var item in placed)
        {
            corners.Add(new Vector3(item.Max.X, item.Min.Y, item.Min.Z));
            corners.Add(new Vector3(item.Min.X, item.Max.Y, item.Min.Z));
            corners.Add(new Vector3(item.Min.X, item.Min.Y, item.Max.Z));
        }

        corners.Sort(static (a, b) =>
        {
            var byHeight = a.Y.CompareTo(b.Y);
            if (byHeight != 0)
                return byHeight;
            var byDepth = a.Z.CompareTo(b.Z);
            return byDepth != 0 ? byDepth : a.X.CompareTo(b.X);
        });
        return corners;
    }

    private bool Fits(IReadOnlyList<PackedItem> placed, Vector3 min, Vector3 size)
    {
        var max = min + size;
        if (max.X > Interior.X + Epsilon || max.Y > Interior.Y + Epsilon || max.Z > Interior.Z + Epsilon)
            return false;

        var carried = 0f;
        foreach (var item in placed)
        {
            if (Overlaps(min, max, item))
                return false;
            if (Math.Abs(item.Max.Y - min.Y) <= Epsilon)
                carried += SharedFloor(min, max, item);
        }

        return min.Y <= Epsilon || carried >= SupportShare * size.X * size.Z;
    }

    private static bool Overlaps(Vector3 min, Vector3 max, PackedItem item) =>
        min.X < item.Max.X - Epsilon && max.X > item.Min.X + Epsilon
                                     && min.Y < item.Max.Y - Epsilon && max.Y > item.Min.Y + Epsilon
                                     && min.Z < item.Max.Z - Epsilon && max.Z > item.Min.Z + Epsilon;

    // The area of the floor plan that the cell from min to max shares with the item.
    private static float SharedFloor(Vector3 min, Vector3 max, PackedItem item) =>
        Math.Max(0f, Math.Min(max.X, item.Max.X) - Math.Max(min.X, item.Min.X))
        * Math.Max(0f, Math.Min(max.Z, item.Max.Z) - Math.Max(min.Z, item.Min.Z));
}
