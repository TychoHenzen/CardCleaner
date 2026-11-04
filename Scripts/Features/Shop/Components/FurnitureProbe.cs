using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     The physics queries behind a placement session: whether a pose is blocked by a solid body, and which placed
///     piece the camera is looking at.
/// </summary>
internal static class FurnitureProbe
{
    /// <summary>How far in front of the camera the pick-up probe reaches, in metres.</summary>
    private const float PickUpReach = 4.5f;

    /// <summary>The collision layer that placed furniture and the shop walls sit on.</summary>
    private const uint FurnitureLayerMask = 1;

    /// <summary>The probe box is this much smaller than the footprint, so a touching wall does not block.</summary>
    private const float Shrink = 0.1f;

    /// <summary>The probe box starts this far above the floor, so the floor does not block.</summary>
    private const float FloorClearance = 0.05f;

    public static bool IsBlocked(World3D world, FurnitureItem item, FurniturePose pose)
    {
        var rect = FurniturePlacementModel.RectOf(item.Footprint, pose);
        var width = rect.MaxX - rect.MinX - Shrink;
        var depth = rect.MaxZ - rect.MinZ - Shrink;
        var height = item.Height - Shrink;
        var shape = new BoxShape3D { Size = new Vector3(width, height, depth) };
        var centre = new Vector3(pose.X, FloorClearance + height / 2f, pose.Z);
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = shape,
            Transform = new Transform3D(Basis.Identity, centre),
            CollisionMask = FurnitureLayerMask
        };
        // The space has no state before its first physics frame, so nothing can be blocking yet.
        return world.DirectSpaceState?.IntersectShape(query, 1).Count > 0;
    }

    public static PlacedFurniture? Aimed(World3D world, Camera3D camera)
    {
        var from = camera.GlobalPosition;
        var to = from - camera.GlobalBasis.Z * PickUpReach;
        var result = world.DirectSpaceState.IntersectRay(
            PhysicsRayQueryParameters3D.Create(from, to, FurnitureLayerMask));
        if (result.Count == 0 || result["collider"].Obj is not Node3D hit)
            return null;

        Node? node = hit;
        while (node != null && node is not PlacedFurniture)
            node = node.GetParent();
        return node as PlacedFurniture;
    }
}
