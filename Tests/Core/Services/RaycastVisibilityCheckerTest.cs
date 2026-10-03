using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Tests.TestUtilities.Fixtures;
using Godot;

namespace CardCleaner.Tests.Core.Services;

/// <summary>
/// Tests for RaycastVisibilityChecker.
/// These tests require the Godot physics runtime to perform raycasts.
/// Note: Physics raycasts may require additional synchronization in tests.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class RaycastVisibilityCheckerTest
{
    private const string Wall = "wall";
    private const float TileSize = 16f;

    private Node2D _root = null!;
    private StaticBody2D _terrainCollider = null!;
    private World2D _world = null!;
    private PhysicsDirectSpaceState2D _spaceState = null!;

    [BeforeTest]
    public void Setup()
    {
        // Create a scene tree for physics
        _root = new Node2D();
        _root.Name = "TestRoot";

        // Create a SubViewport to get our own World2D
        var viewport = new SubViewport();
        viewport.World2D = new World2D();
        _root.AddChild(viewport);

        _terrainCollider = new StaticBody2D();
        viewport.AddChild(_terrainCollider);

        _world = viewport.World2D;
        _spaceState = _world.DirectSpaceState;
    }

    [AfterTest]
    public void Teardown()
    {
        _root.QueueFree();
    }

    [TestCase]
    public void TestCanSeeSameCell()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5);
        GenerateCollisionShapes(gridData);

        var checker = new RaycastVisibilityChecker(_spaceState);
        var cellId = gridData.GetCellId(new Vector2I(2, 2));

        var result = checker.CanSee(cellId, cellId, gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeAdjacentCell()
    {
        var (mapData, gridData) = OpenFloorMap.Create(5, 5);
        GenerateCollisionShapes(gridData);

        var checker = new RaycastVisibilityChecker(_spaceState);
        var from = gridData.GetCellId(new Vector2I(2, 2));
        var to = gridData.GetCellId(new Vector2I(3, 2));

        var result = checker.CanSee(from, to, gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCanSeeAcrossEmptyMap()
    {
        var (mapData, gridData) = OpenFloorMap.Create(10, 10);
        GenerateCollisionShapes(gridData);

        var checker = new RaycastVisibilityChecker(_spaceState);
        var from = gridData.GetCellId(new Vector2I(0, 0));
        var to = gridData.GetCellId(new Vector2I(9, 9));

        var result = checker.CanSee(from, to, gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestCollisionLayerDefaultValue()
    {
        // Verify the default collision layer is layer 16 (bit 15)
        const uint expectedLayer = 1u << 15;
        AssertThat(RaycastVisibilityChecker.OpaqueTerrainCollisionLayer).IsEqual(expectedLayer);
    }

    [TestCase]
    public void TestFromWorldFactory()
    {
        var checker = RaycastVisibilityChecker.FromWorld(_world);
        AssertThat(checker).IsNotNull();
    }

    [TestCase]
    public void TestSameWorldPositionAlwaysVisible()
    {
        var (mapData, gridData) = OpenFloorMap.Create(3, 3);
        GenerateCollisionShapes(gridData);

        var checker = new RaycastVisibilityChecker(_spaceState);
        var pos = new Vector2(TileSize * 1.5f, TileSize * 1.5f);

        // Looking at same position should always return true
        var result = checker.CanSee(pos, pos, gridData);

        AssertBool(result).IsTrue();
    }

    [TestCase]
    public void TestConstructorWithDefaultMask()
    {
        var checker = new RaycastVisibilityChecker(_spaceState);
        // Just verify construction succeeds
        AssertThat(checker).IsNotNull();
    }

    [TestCase]
    public void TestConstructorWithCustomMask()
    {
        const uint customMask = 1u << 5;
        var checker = new RaycastVisibilityChecker(_spaceState, customMask);
        // Just verify construction succeeds
        AssertThat(checker).IsNotNull();
    }

    private void GenerateCollisionShapes(RegularGridMapData gridData)
    {
        TerrainCollisionShapeGenerator.ClearShapes(_terrainCollider);
        TerrainCollisionShapeGenerator.GenerateForRegularGrid(gridData, _terrainCollider, TileSize);
    }
}
