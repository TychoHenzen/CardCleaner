using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Visibility checker that uses Godot's physics raycasting for line-of-sight.
/// More robust for irregular meshes than sampling, but requires collision shapes
/// to be generated for opaque terrain.
/// </summary>
public class RaycastVisibilityChecker : IVisibilityChecker
{
    /// <summary>
    /// Physics collision layer for opaque terrain that blocks visibility.
    /// </summary>
    public const uint OpaqueTerrainCollisionLayer = 1u << 15; // Layer 16

    private readonly PhysicsDirectSpaceState2D _spaceState;
    private readonly uint _collisionMask;

    /// <summary>
    /// Create a raycast visibility checker.
    /// </summary>
    /// <param name="spaceState">The physics space state to use for raycasting.</param>
    /// <param name="collisionMask">Collision mask for opaque terrain. Defaults to OpaqueTerrainCollisionLayer.</param>
    public RaycastVisibilityChecker(
        PhysicsDirectSpaceState2D spaceState,
        uint collisionMask = OpaqueTerrainCollisionLayer)
    {
        _spaceState = spaceState;
        _collisionMask = collisionMask;
    }

    /// <summary>
    /// Create a raycast visibility checker from a World2D.
    /// </summary>
    public static RaycastVisibilityChecker FromWorld(World2D world)
    {
        return new RaycastVisibilityChecker(world.DirectSpaceState);
    }

    public bool CanSee(int fromCellId, int toCellId, IMapData mapData)
    {
        if (fromCellId == toCellId) return true;

        var from = mapData.GetCellCenter(fromCellId);
        var to = mapData.GetCellCenter(toCellId);
        return CanSee(from, to, mapData);
    }

    public bool CanSee(Vector2 fromPosition, Vector2 toPosition, IMapData mapData)
    {
        if (fromPosition.IsEqualApprox(toPosition)) return true;

        // Create raycast query
        var query = PhysicsRayQueryParameters2D.Create(fromPosition, toPosition, _collisionMask);

        // Exclude the source and destination areas if needed
        // (they might have collision shapes but we want to see from/to them)
        query.HitFromInside = false;

        // Perform the raycast
        var result = _spaceState.IntersectRay(query);

        // If no intersection, we can see
        if (result.Count == 0)
            return true;

        // A hit on the destination's own collision shape does not block: an opaque target stays visible,
        // while opaque cells in front of it still block the ray.
        return HitsShapeContaining(result, toPosition);
    }

    private bool HitsShapeContaining(Godot.Collections.Dictionary hit, Vector2 position)
    {
        var pointQuery = new PhysicsPointQueryParameters2D
        {
            Position = position,
            CollisionMask = _collisionMask
        };

        var hitRid = (Rid)hit["rid"];
        var hitShape = (int)hit["shape"];
        foreach (var overlap in _spaceState.IntersectPoint(pointQuery))
        {
            if ((Rid)overlap["rid"] == hitRid && (int)overlap["shape"] == hitShape)
                return true;
        }

        return false;
    }
}
