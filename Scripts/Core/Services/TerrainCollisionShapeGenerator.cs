using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Generates collision shapes for opaque terrain cells.
/// Used with RaycastVisibilityChecker for physics-based line-of-sight.
/// </summary>
public static class TerrainCollisionShapeGenerator
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

        // Set collision layer
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

    /// <summary>
    /// Generate collision shapes for opaque cells in a regular grid map.
    /// </summary>
    /// <param name="mapData">The map data providing cell transparency info.</param>
    /// <param name="parent">The collision object to add shapes to.</param>
    /// <param name="tileSize">Size of each grid cell in world units.</param>
    /// <param name="collisionLayer">The collision layer for the shapes.</param>
    public static void GenerateForRegularGrid(
        RegularGridMapData mapData,
        CollisionObject2D parent,
        float tileSize = 16f,
        uint collisionLayer = RaycastVisibilityChecker.OpaqueTerrainCollisionLayer)
    {
        var bounds = mapData.WorldBounds;

        for (int cellId = 0; cellId < mapData.CellCount; cellId++)
        {
            // Skip transparent cells
            if (mapData.IsTransparent(cellId))
                continue;

            var gridPos = mapData.CellIdToPosition(cellId);
            var worldPos = new Vector2(gridPos.X * tileSize, gridPos.Y * tileSize);

            // Create rectangle shape for the cell
            var shape = new RectangleShape2D();
            shape.Size = new Vector2(tileSize, tileSize);

            // Create shape owner and set transform to cell center
            var ownerId = parent.CreateShapeOwner(parent);
            parent.ShapeOwnerAddShape(ownerId, shape);

            var transform = new Transform2D(0, worldPos + new Vector2(tileSize / 2, tileSize / 2));
            parent.ShapeOwnerSetTransform(ownerId, transform);
        }

        // Set collision layer
        if (parent is Area2D area)
        {
            area.CollisionLayer = collisionLayer;
            area.CollisionMask = 0;
        }
        else if (parent is StaticBody2D staticBody)
        {
            staticBody.CollisionLayer = collisionLayer;
            staticBody.CollisionMask = 0;
        }
    }

    /// <summary>
    /// Create optimized collision shapes by merging adjacent opaque cells.
    /// More efficient for large maps with contiguous opaque regions.
    /// </summary>
    /// <param name="mapData">The map data.</param>
    /// <param name="parent">The collision object to add shapes to.</param>
    /// <param name="tileSize">Size of each grid cell.</param>
    /// <param name="collisionLayer">The collision layer.</param>
    public static void GenerateOptimizedForRegularGrid(
        RegularGridMapData mapData,
        CollisionObject2D parent,
        float tileSize = 16f,
        uint collisionLayer = RaycastVisibilityChecker.OpaqueTerrainCollisionLayer)
    {
        // Use greedy meshing to combine adjacent opaque cells into larger rectangles
        var visited = new HashSet<int>();
        var rects = new List<Rect2I>();

        for (int cellId = 0; cellId < mapData.CellCount; cellId++)
        {
            if (visited.Contains(cellId) || mapData.IsTransparent(cellId))
                continue;

            var startPos = mapData.CellIdToPosition(cellId);
            var rect = ExpandRectangle(mapData, startPos, visited);
            rects.Add(rect);
        }

        // Create shapes from merged rectangles
        foreach (var rect in rects)
        {
            var worldPos = new Vector2(rect.Position.X * tileSize, rect.Position.Y * tileSize);
            var worldSize = new Vector2(rect.Size.X * tileSize, rect.Size.Y * tileSize);

            var shape = new RectangleShape2D();
            shape.Size = worldSize;

            var ownerId = parent.CreateShapeOwner(parent);
            parent.ShapeOwnerAddShape(ownerId, shape);

            var transform = new Transform2D(0, worldPos + worldSize / 2);
            parent.ShapeOwnerSetTransform(ownerId, transform);
        }

        // Set collision layer
        if (parent is Area2D area)
        {
            area.CollisionLayer = collisionLayer;
            area.CollisionMask = 0;
        }
        else if (parent is StaticBody2D staticBody)
        {
            staticBody.CollisionLayer = collisionLayer;
            staticBody.CollisionMask = 0;
        }
    }

    private static Rect2I ExpandRectangle(RegularGridMapData mapData, Vector2I start, HashSet<int> visited)
    {
        var width = mapData.Size.X;
        var height = mapData.Size.Y;

        // Expand width first
        int endX = start.X;
        while (endX < width && IsFreeCell(mapData, visited, start.Y * width + endX))
        {
            endX++;
        }
        int rectWidth = endX - start.X;

        // Then expand height
        int endY = start.Y;
        while (endY < height && IsRowFree(mapData, visited, new RowSpan(endY, start.X, rectWidth), width))
        {
            MarkRowVisited(visited, new RowSpan(endY, start.X, rectWidth), width);
            endY++;
        }

        return new Rect2I(start, new Vector2I(rectWidth, endY - start.Y));
    }

    private static bool IsFreeCell(RegularGridMapData mapData, HashSet<int> visited, int cellId)
    {
        return !visited.Contains(cellId) && !mapData.IsTransparent(cellId);
    }

    private static bool IsRowFree(RegularGridMapData mapData, HashSet<int> visited, RowSpan row, int gridWidth)
    {
        for (int x = row.StartX; x < row.StartX + row.Width; x++)
        {
            if (!IsFreeCell(mapData, visited, row.Y * gridWidth + x))
                return false;
        }

        return true;
    }

    private static void MarkRowVisited(HashSet<int> visited, RowSpan row, int gridWidth)
    {
        for (int x = row.StartX; x < row.StartX + row.Width; x++)
        {
            visited.Add(row.Y * gridWidth + x);
        }
    }

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
}
