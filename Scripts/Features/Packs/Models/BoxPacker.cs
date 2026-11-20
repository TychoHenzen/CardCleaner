using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     Packs items into a box one at a time without overlap. The same items in the same order always land in the same
///     places. <para> Cards lie in four stacks in a pinwheel on the floor (<see cref="CardStacks" />): two flat and two
///     turned, around a small square hole, the whole centred on the floor. A card goes on top of the first stack that
///     still has room, so one stack fills before the next is started and the last card in is always on top of its
///     stack. Cards use the stack places that containers leave free, so a box with containers in it holds fewer cards.
///     </para> <para> A container goes where it rests lowest, then furthest back, then furthest left, in the first of
///     its poses (<see cref="ItemShapes.Poses" />) that fits anywhere: on the floor, or on items below it whose tops
///     carry at least <see cref="SupportShare" /> of its base. The spots tried are the box's back left corner and the
///     corners next to, on and behind every item already in. A booster goes straight on top of the booster put in just
///     before it, so a run of boosters becomes one stack. </para>
/// </summary>
public sealed class BoxPacker
{
    /// <summary>Gap left on each side of an item across the floor, so items side by side never touch.</summary>
    public const float Clearance = 0.002f;

    /// <summary>
    ///     The share of an item's base that the tops below it must carry when it does not stand on the floor.
    /// </summary>
    public const float SupportShare = 0.6f;

    private const float Epsilon = 1e-4f;

    public BoxPacker(Vector3 interior)
    {
        Interior = interior;
        CardStacks = Pinwheel(interior);
    }

    /// <summary>The inner size of the box, which every item must stay within.</summary>
    public Vector3 Interior { get; }

    /// <summary>
    ///     Floor spot and pose of the four card stacks in fill order: for an a x b card cell, flat at (0, 0), turned at
    ///     (a, 0), flat at (b, a) and turned at (0, b), a pinwheel round a (b - a) hole in an (a + b) square.
    /// </summary>
    public IReadOnlyList<(ItemPose Pose, Vector3 Floor)> CardStacks { get; }

    /// <summary>
    ///     Where an item of <paramref name="shape" /> goes, given the items <paramref name="placed" /> so far; null
    ///     when it fits nowhere.
    /// </summary>
    public PackedItem? Place(IReadOnlyList<PackedItem> placed, ItemShape shape)
    {
        if (shape == ItemShape.Card)
            return OnACardStack(placed);

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

    private static (ItemPose Pose, Vector3 Floor)[] Pinwheel(Vector3 interior)
    {
        var flat = Cell(ItemShape.Card, ItemPose.Flat);
        var side = flat.X + flat.Z;
        var origin = new Vector3(Math.Max(0f, (interior.X - side) / 2f), 0f, Math.Max(0f, (interior.Z - side) / 2f));
        return
        [
            (ItemPose.Flat, origin),
            (ItemPose.Turned, origin + new Vector3(flat.X, 0f, 0f)),
            (ItemPose.Flat, origin + new Vector3(flat.Z, 0f, flat.X)),
            (ItemPose.Turned, origin + new Vector3(0f, 0f, flat.Z)),
        ];
    }

    // The card goes on the highest item over the first stack place that still takes it.
    private PackedItem? OnACardStack(IReadOnlyList<PackedItem> placed)
    {
        foreach (var (pose, floor) in CardStacks)
        {
            var size = Cell(ItemShape.Card, pose);
            var min = floor with { Y = TopOver(placed, floor, size) };
            if (Fits(placed, min, size))
                return new PackedItem(ItemShape.Card, pose, min, size);
        }

        return null;
    }

    // The top of the highest item whose floor plan shares any area with the cell at floor.
    private static float TopOver(IReadOnlyList<PackedItem> placed, Vector3 floor, Vector3 size)
    {
        var top = 0f;
        foreach (var item in placed)
        {
            if (item.Max.Y > top && SharedFloor(floor, floor + size, item) > Epsilon * Epsilon)
                top = item.Max.Y;
        }

        return top;
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
        if (min.X < -Epsilon || min.Y < -Epsilon || min.Z < -Epsilon
            || max.X > Interior.X + Epsilon || max.Y > Interior.Y + Epsilon || max.Z > Interior.Z + Epsilon)
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
