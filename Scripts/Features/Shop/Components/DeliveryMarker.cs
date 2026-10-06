using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A delivery area for a room with a ceiling. It owns the occupancy of its slot cells: a cell is taken
///     while any delivered item's collision footprint overlaps it, and is free again once the item has left every
///     cell, was removed from the tree or was freed. Free cells are filled at ground level first. When every cell
///     is taken, an order is placed on top of the lowest stack in the area, as long as the item still fits under
///     <see cref="MaxStackHeight" />. The order is refused only when no cell has that much clearance left.
/// </summary>
public partial class DeliveryMarker : Marker3D
{
    /// <summary>Vertical gap left between a new item and the top of the item it is stacked on.</summary>
    public const float StackGap = 0.02f;

    /// <summary>Smallest height an item counts for, so a flat or shapeless item still occupies a stack level.</summary>
    public const float MinItemHeight = 0.1f;

    private readonly List<Node3D> _delivered = [];

    /// <summary>
    ///     Clearance above the marker, in metres, that stacked deliveries must stay under. Set it to the room's
    ///     ceiling underside measured from the marker.
    /// </summary>
    [Export]
    public float MaxStackHeight { get; set; } = 3.9f;

    /// <summary>
    ///     Where the next item should go: the first free ground cell, otherwise the lowest stack top that still
    ///     leaves room for <paramref name="item" />. Null when the area has no clearance left. Items that no
    ///     longer overlap any cell are forgotten, so only live occupants are ever retained.
    /// </summary>
    /// <param name="slotCount">How many slots the area has.</param>
    /// <param name="slotOffset">Offset of a slot's centre from this marker.</param>
    /// <param name="cellSize">Width (X) and depth (Z) of one slot's cell.</param>
    /// <param name="item">The item about to be delivered, not yet placed in the world. Its origin is taken to be at its feet.</param>
    public Placement? FindPlacement(int slotCount, Func<int, Vector3> slotOffset, Vector2 cellSize, Node3D item)
    {
        var stackTop = new float[slotCount];
        _delivered.RemoveAll(occupant => !Absorb(occupant, stackTop, slotOffset, cellSize));

        var free = Array.FindIndex(stackTop, top => top <= 0f);
        if (free >= 0)
            return new Placement(free, 0f);

        var bounds = LocalBounds(item);
        var height = Mathf.Max(bounds.End.Y, MinItemHeight);
        var lowest = Array.IndexOf(stackTop, stackTop.Min());
        var lift = stackTop[lowest] + StackGap;
        return lift + height <= MaxStackHeight ? new Placement(lowest, lift) : null;
    }

    /// <summary>Starts treating <paramref name="item" /> as an occupant of this area.</summary>
    public void Track(Node3D item) => _delivered.Add(item);

    /// <summary>
    ///     Raises the stack top of every cell the occupant overlaps. Returns false when the occupant is gone or
    ///     overlaps no cell any more.
    /// </summary>
    private bool Absorb(Node3D occupant, float[] stackTop, Func<int, Vector3> slotOffset, Vector2 cellSize)
    {
        if (!IsLive(occupant))
            return false;

        var box = WorldBounds(occupant);
        var footprint = new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z);
        var top = Mathf.Max(box.End.Y - GlobalPosition.Y, MinItemHeight);
        var overlapsAnyCell = false;
        for (var slot = 0; slot < stackTop.Length; slot++)
        {
            if (!Overlaps(footprint, GlobalPosition + slotOffset(slot), cellSize))
                continue;
            stackTop[slot] = Mathf.Max(stackTop[slot], top);
            overlapsAnyCell = true;
        }

        return overlapsAnyCell;
    }

    private static bool IsLive(Node3D item) =>
        GodotObject.IsInstanceValid(item) && item.IsInsideTree() && !item.IsQueuedForDeletion();

    private static bool Overlaps(Rect2 footprint, Vector3 cellCentre, Vector2 cellSize)
    {
        var left = cellCentre.X - cellSize.X / 2f;
        var back = cellCentre.Z - cellSize.Y / 2f;
        return footprint.Position.X < left + cellSize.X && footprint.End.X > left
                                                       && footprint.Position.Y < back + cellSize.Y &&
                                                       footprint.End.Y > back;
    }

    /// <summary>
    ///     World bounds of every collision shape under the item. An item without any shape counts as a point at
    ///     its origin.
    /// </summary>
    private static Aabb WorldBounds(Node3D item)
    {
        Aabb? bounds = null;
        foreach (var node in item.FindChildren("*", nameof(CollisionShape3D), true, false))
        {
            if (node is not CollisionShape3D { Disabled: false, Shape: { } shape } collider)
                continue;

            var world = collider.GlobalTransform * ShapeBounds(shape);
            bounds = bounds.HasValue ? bounds.Value.Merge(world) : world;
        }

        return bounds ?? new Aabb(item.GlobalPosition, Vector3.Zero);
    }

    /// <summary>Bounds of the item's collision shapes in its own space, usable before it enters the tree.</summary>
    private static Aabb LocalBounds(Node3D item)
    {
        Aabb? bounds = null;
        foreach (var node in item.FindChildren("*", nameof(CollisionShape3D), true, false))
        {
            if (node is not CollisionShape3D { Disabled: false, Shape: { } shape } collider)
                continue;

            var toItem = Transform3D.Identity;
            for (Node? step = collider; step is Node3D spatial && spatial != item; step = step.GetParent())
                toItem = spatial.Transform * toItem;

            var local = toItem * ShapeBounds(shape);
            bounds = bounds.HasValue ? bounds.Value.Merge(local) : local;
        }

        return bounds ?? new Aabb(Vector3.Zero, Vector3.Zero);
    }

    private static Aabb ShapeBounds(Shape3D shape) =>
        shape is BoxShape3D box
            ? new Aabb(-box.Size / 2f, box.Size)
            : shape.GetDebugMesh().GetAabb();
}

/// <summary>A slot in a <see cref="DeliveryMarker" /> and how far above the marker's ground the item starts.</summary>
public readonly record struct Placement(int Slot, float Lift);
