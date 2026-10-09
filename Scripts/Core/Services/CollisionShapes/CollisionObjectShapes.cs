using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Godot-only helpers for the collision objects that hold generated terrain shapes.
/// </summary>
public static class CollisionObjectShapes
{
    /// <summary>
    /// Clear all collision shapes from a collision object.
    /// </summary>
    public static void ClearShapes(CollisionObject2D parent)
    {
        var ownerIds = parent.GetShapeOwners();
        foreach (var ownerId in ownerIds)
        {
            parent.ShapeOwnerClearShapes((uint)ownerId);
            parent.RemoveShapeOwner((uint)ownerId);
        }
    }

    /// <summary>
    /// Put a terrain collision object on the given layer. The mask is cleared, so it is detected but detects nothing.
    /// </summary>
    public static void ApplyTerrainLayer(CollisionObject2D parent, uint collisionLayer)
    {
        if (parent is Area2D area)
        {
            area.CollisionLayer = collisionLayer;
            area.CollisionMask = 0; // Don't detect anything, just be detected
        }
        else if (parent is StaticBody2D staticBody)
        {
            staticBody.CollisionLayer = collisionLayer;
            staticBody.CollisionMask = 0;
        }
    }
}
