using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;
using CardCleaner.Scripts.Features.Shop.Ordering;

namespace CardCleaner.Scripts.Features.Shop.Furniture;

/// <summary>
///     Puts unopened furniture boxes on the floor: the scene of the <see cref="Boxes" /> catalog row whose id is the
///     item's, added under <see cref="Root" /> and tracked on <see cref="Marker" /> when there is one. A restored
///     save and a packed piece both make their boxes here.
/// </summary>
internal readonly struct FurnitureBoxSpawn
{
    public FurnitureBoxSpawn(OrderCatalog boxes, Node root, DeliveryMarker? marker)
    {
        Boxes = boxes;
        Root = root;
        Marker = marker;
    }

    public OrderCatalog Boxes { get; }

    public Node Root { get; }

    public DeliveryMarker? Marker { get; }

    /// <summary>Height of a box's origin above the shop floor.</summary>
    // ASSUMPTION: boxes sit on the shop floor at a fixed height; no export records a delivery height (Risk Low).
    public const float BoxHeight = 0.5f;

    /// <summary>
    ///     Adds the box of <paramref name="id" /> at floor point <paramref name="spot" /> (x, z). Logs an error and
    ///     returns null when the catalog has no 3D box scene for the id.
    /// </summary>
    public Node3D? At(string id, Vector2 spot)
    {
        if (Build(id) is not { } box)
            return null;

        Root.AddChild(box);
        box.GlobalPosition = new Vector3(spot.X, BoxHeight, spot.Y);
        // Only OrderingService tracks a box it delivers; a box put down any other way must count as an occupant too.
        Marker?.Track(box);
        return box;
    }

    /// <summary>
    ///     Adds the box of <paramref name="id" /> on the first free cell of the marker's delivery area, where an
    ///     order of it would arrive. With no marker, or no free cell, it goes to <paramref name="fallback" /> (x, z).
    /// </summary>
    public Node3D? OnDeliveryArea(string id, Vector2 fallback)
    {
        if (Marker is not { } marker)
            return At(id, fallback);
        if (Build(id) is not { } box)
            return null;

        var cell = marker.FindPlacement(box);
        Root.AddChild(box);
        box.GlobalPosition = cell is { } place
            ? marker.GlobalPosition + marker.SlotOffset(place.Slot) + Vector3.Up * place.Lift
            : new Vector3(fallback.X, BoxHeight, fallback.Y);
        marker.Track(box);
        return box;
    }

    // The box scene of the id, built but not in the tree, or null with the reason logged.
    private Node3D? Build(string id)
    {
        var orderItem = Boxes.Items.FirstOrDefault(entry => entry.Id == id);
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
