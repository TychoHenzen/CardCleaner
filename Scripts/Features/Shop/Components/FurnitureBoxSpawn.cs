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
        var orderItem = boxes.Items.FirstOrDefault(entry => entry.Id == id);
        if (orderItem?.Scene == null)
        {
            ILog.Error($"Furniture box '{id}' is not in the box order catalog");
            return null;
        }

        var instance = orderItem.Scene.Instantiate();
        if (instance is not Node3D box)
        {
            instance.Free();
            ILog.Error($"Furniture box '{id}' is not a 3D scene");
            return null;
        }

        root.AddChild(box);
        box.GlobalPosition = new Vector3(x, BoxHeight, z);
        // Only OrderingService tracks a box it delivers; a box put down any other way must count as an occupant too.
        marker?.Track(box);
        return box;
    }
}
