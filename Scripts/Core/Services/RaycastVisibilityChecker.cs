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

    public bool CanSee(Vector2 from, Vector2 to, IMapData mapData)
    {
        if (from.IsEqualApprox(to)) return true;

        // Create raycast query
        var query = PhysicsRayQueryParameters2D.Create(from, to, _collisionMask);

        // Exclude the source and destination areas if needed
        // (they might have collision shapes but we want to see from/to them)
        query.HitFromInside = false;

        // Perform the raycast
        var result = _spaceState.IntersectRay(query);

        // If no intersection, we can see
        if (result.Count == 0)
            return true;

        // Check if the intersection point is past the target
        // (this handles the case where we're looking at an opaque cell - we should see it)
        var hitPosition = (Vector2)result["position"];
        var toTarget = to - from;
        var toHit = hitPosition - from;

        // If we hit something after the target, we can see the target
        return toHit.LengthSquared() >= toTarget.LengthSquared() - 0.01f;
    }
}
