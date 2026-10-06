using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The open build space joins the workshop room through a doorway in its east wall, to the right of the
/// cabinet, stays clear of the ordering room and the shop interior, and can be walked from the cabinet into
/// every room by the production player.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopBuildSpaceSceneTest
{
    private const float PlayerStartHeight = 1.2f;
    private const int ClearanceCells = 4;
    private const float RoomEastEdge = 6f;
    private const float WallThickness = 0.4f;
    private const float DoorwayHalfWidth = 2f;
    private const float WallProbeHeight = 1f;
    // The wall pack art sits about 0.11 m past the 4 m panel edge; that sliver is visual only.
    private const float PanelArtOverhang = 0.2f;
    private const float DoorwayBottom = 0.1f;
    private const float DoorwayTop = 3.9f;
    private const double WalkTimeScale = 4.0;
    private const float MaxHeightVariation = 0.05f;
    private const float MaxAirborneShare = 0.01f;
    private const float FallLimitY = -1f;
    private const float MinimumDistanceFromShop = 30f;

    private static readonly Vector2[] CabinetToDoorway = [new(4.5f, -3.5f), new(5.2f, -1.2f), new(6.6f, 0f)];

    private Node3D _shop = null!;
    private Node3D _workshop = null!;
    private WorkshopGrid _grid = null!;
    private CharacterBody3D _player = null!;
    private float _lowest = float.MaxValue;
    private float _highest = float.MinValue;
    private int _airborneFrames;
    private int _frames;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _workshop = _shop.GetNode<Node3D>("World/Workshop");
        _grid = _workshop.GetNode<WorkshopGrid>("WorkshopGrid");
        _player = _shop.GetNode<CharacterBody3D>("Player");

        // The test scene is not the current scene, so hand the scene-owned settings to the player directly.
        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        Input.ActionRelease("ui_up");
        Engine.TimeScale = 1.0;
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GridSitsToTheRightOfTheCabinetBeyondTheEastWallAndFarFromTheShop()
    {
        var cabinet = _workshop.GetNode<Node3D>("Cabinet").GlobalPosition;
        var doorway = _grid.GetNode<Node3D>("Doorway").GlobalPosition;
        var eastEdge = _workshop.GlobalPosition.X + RoomEastEdge;

        AssertBool(doorway.X > cabinet.X).IsTrue();
        AssertBool(_grid.GetUsedCellsByItem(WorkshopGridPaths.FloorItem).All(cell => _grid.CellToWorld(cell).X > eastEdge)).IsTrue();
        AssertBool(_grid.GetUsedCells().All(cell => _grid.CellToWorld(cell).X > eastEdge - WallThickness)).IsTrue();
        AssertBool(_grid.CellToWorld(_grid.GetUsedCells()[0]).X - _player.GlobalPosition.X > MinimumDistanceFromShop).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GridDoesNotOverlapAnyShopInteriorGeometry()
    {
        var cells = _grid.GetUsedCells().Select(c => _grid.CellToWorld(c)).ToList();
        var half = _grid.CellSize / 2f;
        var low = cells.Aggregate((a, b) => new Vector3(Mathf.Min(a.X, b.X), Mathf.Min(a.Y, b.Y), Mathf.Min(a.Z, b.Z))) - half;
        var high = cells.Aggregate((a, b) => new Vector3(Mathf.Max(a.X, b.X), Mathf.Max(a.Y, b.Y), Mathf.Max(a.Z, b.Z))) + half;
        var gridBounds = new Aabb(low, high - low).Merge(WorldBounds(_grid.GetNode<MeshInstance3D>("Ceiling/Mesh")));

        var shopMeshes = _shop.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>()
            .Where(m => !_workshop.IsAncestorOf(m) && m.Mesh != null);

        foreach (var mesh in shopMeshes)
            AssertBool(gridBounds.Intersects(WorldBounds(mesh))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemainingEastWallPanelsLeaveTheDoorwayFree()
    {
        var doorway = new Aabb(
            new Vector3(_workshop.GlobalPosition.X + RoomEastEdge - 0.5f, DoorwayBottom, _workshop.GlobalPosition.Z - DoorwayHalfWidth + PanelArtOverhang),
            new Vector3(1f, DoorwayTop - DoorwayBottom, 2f * (DoorwayHalfWidth - PanelArtOverhang)));

        foreach (var mesh in _workshop.GetNode("Room/Walls/EastWall").FindChildren("*", nameof(MeshInstance3D), true, false).OfType<MeshInstance3D>())
            AssertBool(doorway.Intersects(WorldBounds(mesh))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EastWallIsOpenOnlyAcrossTheDoorway()
    {
        var wallX = _workshop.GlobalPosition.X + RoomEastEdge - 0.2f;

        AssertBool(HitsWall(wallX, 0f)).IsFalse();
        AssertBool(HitsWall(wallX, DoorwayHalfWidth - 0.3f)).IsFalse();
        AssertBool(HitsWall(wallX, -DoorwayHalfWidth + 0.3f)).IsFalse();
        AssertBool(HitsWall(wallX, DoorwayHalfWidth + 0.3f)).IsTrue();
        AssertBool(HitsWall(wallX, -DoorwayHalfWidth - 0.3f)).IsTrue();
        AssertBool(HitsWall(wallX, 5f)).IsTrue();
        AssertBool(HitsWall(wallX, -5f)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryRoomIsReachableFromTheDoorwayWithRoomForThePlayer()
    {
        var paths = new WorkshopGridPaths(_grid, ClearanceCells);
        var reachable = paths.ReachableFrom(paths.CellOf(_grid.GetNode<Node3D>("Doorway").GlobalPosition));
        var rooms = _grid.GetNode("Rooms").GetChildren().OfType<Marker3D>().ToList();

        AssertBool(rooms.Count >= 3).IsTrue();
        foreach (var room in rooms)
            AssertBool(reachable.Contains(paths.CellOf(room.GlobalPosition))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryClearCellIsReachableAndNoHallwayIsTighterThanThePlayer()
    {
        var paths = new WorkshopGridPaths(_grid, ClearanceCells);
        var reachable = paths.ReachableFrom(paths.CellOf(_grid.GetNode<Node3D>("Doorway").GlobalPosition));

        AssertThat(reachable.Count).IsEqual(paths.Clear.Count);
        AssertThat(paths.CountFloorCellsTooTight(ClearanceCells)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerWalksFromTheCabinetThroughEveryRoomWithoutFalling()
    {
        // Same fixed physics step, more steps per real second: the long route would otherwise run in real time.
        Engine.TimeScale = WalkTimeScale;
        _player.GlobalPosition = new Vector3(_workshop.GlobalPosition.X, PlayerStartHeight, _workshop.GlobalPosition.Z - 3.5f);
        await Settle();

        foreach (var waypoint in CabinetToDoorway.Select(p => p + new Vector2(_workshop.GlobalPosition.X, _workshop.GlobalPosition.Z)))
            await WalkAndAssert(waypoint);

        var paths = new WorkshopGridPaths(_grid, ClearanceCells);
        var here = paths.CellOf(_player.GlobalPosition);
        foreach (var room in _grid.GetNode("Rooms").GetChildren().OfType<Marker3D>().OrderBy(r => r.GlobalPosition.DistanceTo(_player.GlobalPosition)))
        {
            var target = paths.CellOf(room.GlobalPosition);
            foreach (var waypoint in paths.StraightenPath(paths.ShortestPath(here, target)))
                await WalkAndAssert(paths.WorldXz(waypoint));
            here = target;
        }

        GD.Print($"[workshop-walk] frames {_frames}, airborne {_airborneFrames}, y {_lowest:F3}..{_highest:F3}");
        AssertBool(_highest - _lowest < MaxHeightVariation).IsTrue();
        AssertBool(_airborneFrames <= _frames * MaxAirborneShare).IsTrue();
    }

    private static Aabb WorldBounds(MeshInstance3D mesh)
    {
        return mesh.GlobalTransform * mesh.Mesh.GetAabb();
    }

    private async Task WalkAndAssert(Vector2 waypoint)
    {
        var walk = await PlayerWalker.WalkTo(_player, waypoint);

        AssertBool(walk.Arrived).IsTrue();
        AssertBool(_player.GlobalPosition.Y > FallLimitY).IsTrue();
        _lowest = Mathf.Min(_lowest, walk.MinY);
        _highest = Mathf.Max(_highest, walk.MaxY);
        _airborneFrames += walk.AirborneFrames;
        _frames += walk.Frames;
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 10; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }

    private bool HitsWall(float wallX, float zOffset)
    {
        var z = _workshop.GlobalPosition.Z + zOffset;
        var query = PhysicsRayQueryParameters3D.Create(
            new Vector3(wallX - 1f, WallProbeHeight, z), new Vector3(wallX + 1f, WallProbeHeight, z));
        return _shop.GetWorld3D().DirectSpaceState.IntersectRay(query).Count > 0;
    }
}
