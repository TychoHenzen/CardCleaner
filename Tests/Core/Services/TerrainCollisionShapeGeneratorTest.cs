using System.Linq;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;
using static GdUnit4.Assertions;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Tests for TerrainCollisionShapeGenerator.
/// Verifies collision shape generation for opaque terrain cells.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TerrainCollisionShapeGeneratorTest
{
    private const string Floor = "floor";
    private const string Wall = "wall";

    private static int GetShapeOwnerCount(CollisionObject2D body) => body.GetShapeOwners().Count();

    [TestCase]
    public void TestGeneratesShapesForOpaqueCell()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        // Make center cell opaque (wall)
        SetWall(mapData, 1, 1);

        var staticBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody);

        // Should have exactly one shape owner (for the wall)
        AssertThat(GetShapeOwnerCount(staticBody)).IsEqual(1);

        staticBody.Free();
    }

    [TestCase]
    public void TestNoShapesForTransparentCells()
    {
        var (_, gridData) = CreateSimpleMap(3, 3);
        // All cells are floors (transparent)

        var staticBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody);

        // Should have no shape owners
        AssertThat(GetShapeOwnerCount(staticBody)).IsEqual(0);

        staticBody.Free();
    }

    [TestCase]
    public void TestMultipleOpaqueShapes()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5);
        // Create separate wall clusters
        SetWall(mapData, 0, 0);
        SetWall(mapData, 4, 4);

        var staticBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody);

        // Should have two shape owners
        AssertThat(GetShapeOwnerCount(staticBody)).IsEqual(2);

        staticBody.Free();
    }

    [TestCase]
    public void TestSetsCollisionLayer()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        SetWall(mapData, 1, 1);

        var staticBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody);

        // Should set the collision layer to OpaqueTerrainCollisionLayer
        AssertThat(staticBody.CollisionLayer).IsEqual(RaycastVisibilityChecker.OpaqueTerrainCollisionLayer);
        AssertThat(staticBody.CollisionMask).IsEqual(0u);

        staticBody.Free();
    }

    [TestCase]
    public void TestSetsCollisionLayerForArea2D()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        SetWall(mapData, 1, 1);

        var area = new Area2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, area);

        // Should set the collision layer to OpaqueTerrainCollisionLayer
        AssertThat(area.CollisionLayer).IsEqual(RaycastVisibilityChecker.OpaqueTerrainCollisionLayer);
        AssertThat(area.CollisionMask).IsEqual(0u);

        area.Free();
    }

    [TestCase]
    public void TestCustomCollisionLayer()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        SetWall(mapData, 1, 1);

        var staticBody = new StaticBody2D();
        const uint customLayer = 1u << 5; // Layer 6
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody, collisionLayer: customLayer);

        AssertThat(staticBody.CollisionLayer).IsEqual(customLayer);

        staticBody.Free();
    }

    [TestCase]
    public void TestClearShapes()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        SetWall(mapData, 1, 1);

        var staticBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody);

        // Verify shapes were created
        AssertThat(GetShapeOwnerCount(staticBody)).IsGreater(0);

        // Clear shapes
        TerrainCollisionShapeGenerator.ClearShapes(staticBody);

        // Verify shapes are gone
        AssertThat(GetShapeOwnerCount(staticBody)).IsEqual(0);

        staticBody.Free();
    }

    [TestCase]
    public void TestOptimizedMergingReducesShapeCount()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 5);
        // Create a 3x3 block of walls (9 cells)
        for (var y = 1; y <= 3; y++)
        for (var x = 1; x <= 3; x++)
            SetWall(mapData, x, y);

        var regularBody = new StaticBody2D();
        var optimizedBody = new StaticBody2D();

        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, regularBody);
        TerrainCollisionShapeGenerator.GenerateOptimizedForRegularGrid(gridData, optimizedBody);

        // Regular should have 9 shapes (one per cell)
        var regularCount = GetShapeOwnerCount(regularBody);
        // Optimized should have fewer (merged rectangles)
        var optimizedCount = GetShapeOwnerCount(optimizedBody);

        AssertThat(regularCount).IsEqual(9);
        AssertThat(optimizedCount).IsLess(regularCount);

        regularBody.Free();
        optimizedBody.Free();
    }

    [TestCase]
    public void TestOptimizedMergingMergesContiguousRow()
    {
        var (mapData, gridData) = CreateSimpleMap(5, 1);
        // Create a full row of walls
        for (var x = 0; x < 5; x++)
            SetWall(mapData, x, 0);

        var optimizedBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateOptimizedForRegularGrid(gridData, optimizedBody);

        // Should merge into a single rectangle
        AssertThat(GetShapeOwnerCount(optimizedBody)).IsEqual(1);

        optimizedBody.Free();
    }

    [TestCase]
    public void TestOptimizedMergingHandlesSeparateRegions()
    {
        var (mapData, gridData) = CreateSimpleMap(7, 1);
        // Create two separate wall regions
        SetWall(mapData, 0, 0);
        SetWall(mapData, 1, 0);
        // Gap at index 2, 3
        SetWall(mapData, 4, 0);
        SetWall(mapData, 5, 0);
        SetWall(mapData, 6, 0);

        var optimizedBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateOptimizedForRegularGrid(gridData, optimizedBody);

        // Should have 2 rectangles (one for each contiguous region)
        AssertThat(GetShapeOwnerCount(optimizedBody)).IsEqual(2);

        optimizedBody.Free();
    }

    [TestCase]
    public void TestShapePositionMatchesTilePosition()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        SetWall(mapData, 1, 1);
        const float tileSize = 16f;

        var staticBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody, tileSize);

        // Get the shape transform
        var ownerIds = staticBody.GetShapeOwners();
        AssertThat(GetShapeOwnerCount(staticBody)).IsEqual(1);

        var ownerId = (uint)(int)ownerIds[0];
        var transform = staticBody.ShapeOwnerGetTransform(ownerId);

        // Shape center should be at (1.5, 1.5) * tileSize = (24, 24)
        var expectedCenter = new Vector2(1 * tileSize + tileSize / 2, 1 * tileSize + tileSize / 2);
        AssertThat(transform.Origin).IsEqual(expectedCenter);

        staticBody.Free();
    }

    [TestCase]
    public void TestShapeSizeMatchesTileSize()
    {
        var (mapData, gridData) = CreateSimpleMap(3, 3);
        SetWall(mapData, 1, 1);
        const float tileSize = 32f;

        var staticBody = new StaticBody2D();
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, staticBody, tileSize);

        // Get the shape
        var ownerIds = staticBody.GetShapeOwners();
        var ownerId = (uint)(int)ownerIds[0];
        var shape = staticBody.ShapeOwnerGetShape(ownerId, 0) as RectangleShape2D;

        AssertThat(shape).IsNotNull();
        AssertThat(shape!.Size).IsEqual(new Vector2(tileSize, tileSize));

        staticBody.Free();
    }

    private static (SimpleMapData mapData, RegularGridMapData gridData) CreateSimpleMap(int width, int height)
    {
        var mapData = new SimpleMapData
        {
            TileIds = new string[height, width],
            Size = new Vector2I(width, height),
            PlayerStart = new Vector2I(0, 0)
        };

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            mapData.TileIds[y, x] = Floor;
            mapData.PassableTiles.Add(new Vector2I(x, y));
        }

        var gridData = new RegularGridMapData(mapData);
        return (mapData, gridData);
    }

    /// <summary>
    /// Sets a tile as a wall (not passable, not transparent).
    /// Updates TileIds and removes from PassableTiles.
    /// </summary>
    private static void SetWall(SimpleMapData mapData, int x, int y)
    {
        mapData.TileIds[y, x] = Wall;
        mapData.PassableTiles.Remove(new Vector2I(x, y));
    }
}
