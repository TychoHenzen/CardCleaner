using System;
using System.Collections.Generic;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A ground-level delivery area for a room with a ceiling. It holds a single layer of delivery slots,
///     so a tall item never lands inside another or the ceiling. It owns the occupancy of its slots: a slot
///     is taken while any delivered item's collision footprint overlaps the slot's cell, and is free again once
///     the item has left every cell, was removed from the tree or was freed. When every slot is taken the
///     order is refused.
/// </summary>
public partial class DeliveryMarker : Marker3D
{
    private readonly List<Node3D> _delivered = [];

    /// <summary>
    ///     First slot whose cell no delivered item overlaps, or -1 when the area is full. Items that no longer
    ///     overlap any cell are forgotten, so only live occupants are ever retained.
    /// </summary>
    /// <param name="slotCount">How many slots the area has.</param>
    /// <param name="slotOffset">Offset of a slot's centre from this marker.</param>
    /// <param name="cellSize">Width (X) and depth (Z) of one slot's cell.</param>
    public int FindFreeSlot(int slotCount, Func<int, Vector3> slotOffset, Vector2 cellSize)
    {
        var occupied = new bool[slotCount];
        _delivered.RemoveAll(item =>
        {
            if (!IsLive(item))
                return true;

            var footprint = FootprintOnGround(item);
            var overlapsAnyCell = false;
            for (var slot = 0; slot < slotCount; slot++)
            {
                var centre = GlobalPosition + slotOffset(slot);
                if (!Overlaps(footprint, centre, cellSize))
                    continue;
                occupied[slot] = true;
                overlapsAnyCell = true;
            }

            return !overlapsAnyCell;
        });

        return Array.IndexOf(occupied, false);
    }

    /// <summary>Starts treating <paramref name="item" /> as an occupant of this area.</summary>
    public void Track(Node3D item) => _delivered.Add(item);

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
    ///     Ground-plane bounds (X, Z) of every collision shape under the item. An item without any shape
    ///     counts as a point at its origin.
    /// </summary>
    private static Rect2 FootprintOnGround(Node3D item)
    {
        Aabb? bounds = null;
        foreach (var node in item.FindChildren("*", nameof(CollisionShape3D), true, false))
        {
            if (node is not CollisionShape3D { Disabled: false, Shape: { } shape } collider)
                continue;

            var world = collider.GlobalTransform * ShapeBounds(shape);
            bounds = bounds.HasValue ? bounds.Value.Merge(world) : world;
        }

        var box = bounds ?? new Aabb(item.GlobalPosition, Vector3.Zero);
        return new Rect2(box.Position.X, box.Position.Z, box.Size.X, box.Size.Z);
    }

    private static Aabb ShapeBounds(Shape3D shape) =>
        shape is BoxShape3D box
            ? new Aabb(-box.Size / 2f, box.Size)
            : shape.GetDebugMesh().GetAabb();
}
