using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The top-down footprints of delivered items: the world bounds of each collision shape, triggers included, so a
/// scene test can assert that two items never share ground seen from above.
/// </summary>
internal static class DeliveredFootprints
{
    /// <summary>
    /// Every pair of shapes, one from each of two different items, whose world bounds overlap seen from above.
    /// Holder triggers count too: they are what the player reads as an item's holders.
    /// </summary>
    internal static List<string> OverlapsBetween(IReadOnlyList<Node3D> items)
    {
        var footprints = items.Select(Footprints).ToList();
        var overlaps = new List<string>();
        for (var a = 0; a < footprints.Count; a++)
        for (var b = a + 1; b < footprints.Count; b++)
            overlaps.AddRange(
                from first in footprints[a]
                from second in footprints[b]
                where first.Area.Intersects(second.Area)
                select $"item {a} {first.Path} over item {b} {second.Path}");
        return overlaps;
    }

    /// <summary>
    /// The top-down bounds of every enabled collision shape under an item, with each shape's node path.
    /// </summary>
    internal static List<(string Path, Rect2 Area)> Footprints(Node3D item) =>
        item.FindChildren("*", nameof(CollisionShape3D), true, false)
            .OfType<CollisionShape3D>()
            .Where(collider => collider is { Disabled: false, Shape: not null })
            .Select(collider =>
            {
                var bounds = collider.GlobalTransform * collider.Shape!.GetDebugMesh().GetAabb();
                var area = new Rect2(bounds.Position.X, bounds.Position.Z, bounds.Size.X, bounds.Size.Z);
                return (item.GetPathTo(collider.GetParent()).ToString(), area);
            })
            .ToList();
}
