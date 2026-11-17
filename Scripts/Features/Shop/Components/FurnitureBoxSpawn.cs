using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     Puts an unopened furniture box on the floor: the scene of the box order catalog row whose id is the item's.
///     A restored save and a packed piece both make their boxes here.
/// </summary>
internal static class FurnitureBoxSpawn
{
    /// <summary>Height of a box's origin above the shop floor.</summary>
    // ASSUMPTION: boxes sit on the shop floor at a fixed height; no export records a delivery height (Risk Low).
    public const float BoxHeight = 0.5f;

    /// <summary>
    ///     Adds the box of <paramref name="id" /> under <paramref name="root" /> at floor point
    ///     (<paramref name="x" />, <paramref name="z" />) and tracks it on <paramref name="marker" />. Logs an error
    ///     and returns null when the catalog has no 3D box scene for the id.
    /// </summary>
    public static Node3D? At(OrderCatalog boxes, string id, Node root, float x, float z, DeliveryMarker? marker)
    {
        if (Build(boxes, id) is not { } box)
            return null;

        root.AddChild(box);
        box.GlobalPosition = new Vector3(x, BoxHeight, z);
        // Only OrderingService tracks a box it delivers; a box put down any other way must count as an occupant too.
        marker?.Track(box);
        return box;
    }

    /// <summary>
    ///     Adds the box of <paramref name="id" /> under <paramref name="root" /> on the first free cell of
    ///     <paramref name="marker" />'s delivery area, where an order of it would arrive. With no marker, or no free
    ///     cell, it goes to floor point (<paramref name="x" />, <paramref name="z" />) instead.
    /// </summary>
    public static Node3D? OnDeliveryArea(OrderCatalog boxes, string id, Node root, DeliveryMarker? marker, float x,
        float z)
    {
        if (marker == null)
            return At(boxes, id, root, x, z, null);
        if (Build(boxes, id) is not { } box)
            return null;

        var cell = marker.FindPlacement(box);
        root.AddChild(box);
        box.GlobalPosition = cell is { } place
            ? marker.GlobalPosition + marker.SlotOffset(place.Slot) + Vector3.Up * place.Lift
            : new Vector3(x, BoxHeight, z);
        marker.Track(box);
        return box;
    }

    // The box scene of the id, built but not in the tree, or null with the reason logged.
    private static Node3D? Build(OrderCatalog boxes, string id)
    {
        var orderItem = boxes.Items.FirstOrDefault(entry => entry.Id == id);
        if (orderItem?.Scene == null)
        {
            ILog.Error($"Furniture box '{id}' is not in the box order catalog");
            return null;
        }

        var instance = orderItem.Scene.Instantiate();
        if (instance is Node3D box)
            return box;

        instance.Free();
        ILog.Error($"Furniture box '{id}' is not a 3D scene");
        return null;
    }
}
