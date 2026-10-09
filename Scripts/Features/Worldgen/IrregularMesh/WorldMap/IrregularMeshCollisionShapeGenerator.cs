using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Generates collision shapes for the opaque quads of an irregular mesh.
/// Used with RaycastVisibilityChecker for physics-based line-of-sight.
/// </summary>
public static class IrregularMeshCollisionShapeGenerator
{
    /// <summary>
    /// Generate collision shapes for all opaque cells in an irregular mesh.
    /// </summary>
    /// <param name="mapData">The map data providing cell transparency info.</param>
    /// <param name="parent">The collision object to add shapes to.</param>
    /// <param name="collisionLayer">The collision layer for the shapes.</param>
    public static void GenerateForIrregularMesh(
        IrregularMeshMapData mapData,
        CollisionObject2D parent,
        uint collisionLayer = RaycastVisibilityChecker.OpaqueTerrainCollisionLayer)
    {
        var mesh = mapData.GetMesh();
        var worldScale = mapData.WorldScale;
        var worldOffset = mapData.WorldOffset;

        foreach (var quad in mesh.Quads)
        {
            // Skip transparent cells
            if (mapData.IsTransparent(quad.Id))
                continue;

            // Create convex polygon shape from quad corners
            var corners = quad.GetCornerPositions();
            var worldCorners = new Vector2[corners.Length];
            for (int i = 0; i < corners.Length; i++)
            {
                worldCorners[i] = corners[i] * worldScale + worldOffset;
            }

            var shape = new ConvexPolygonShape2D();
            shape.Points = worldCorners;

            // Add shape to collision object
            var ownerId = parent.CreateShapeOwner(parent);
            parent.ShapeOwnerAddShape(ownerId, shape);
        }

        CollisionObjectShapes.ApplyTerrainLayer(parent, collisionLayer);
    }
}
