using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

[TestSuite]
[RequireGodotRuntime]
public class ShopGrayboxWorkshopBuildSpaceSceneTest
{
    private const string ScenePath = "res://Scenes/Graybox/ShopGraybox.tscn";
    private const int ClearanceCells = 4;
    private const float PlayerStartHeight = 1.2f;
    private const float MaxHeightVariation = 0.05f;
    private const float FallLimitY = -1f;

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
        _shop = GD.Load<PackedScene>(ScenePath).Instantiate<Node3D>();
        _workshop = _shop.GetNode<Node3D>("World/PortalWorkshop");
        _grid = _workshop.GetNode<WorkshopGrid>("WorkshopGrid");
        _player = _shop.GetNode<CharacterBody3D>("Player");

        ServiceLocator.ResetForTesting();
        ServiceLocator.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));

        AddNode(_shop);
        await Settle();
    }

    [AfterTest]
    public void Teardown()
    {
        Input.ActionRelease("ui_up");
        Engine.TimeScale = 1f;
        if (GodotObject.IsInstanceValid(_shop))
            _shop.Free();
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GrayboxGridHasSmallCellsAndAReachableForkedLayout()
    {
        AssertThat(_grid.CellSize).IsEqual(new Vector3(0.2f, 0.2f, 0.2f));
        AssertBool(_grid.GetUsedCellsByItem(WorkshopGridPaths.FloorItem).Count > 0).IsTrue();
        AssertBool(_grid.GetUsedCellsByItem(WorkshopGridPaths.WallItem).Count > 0).IsTrue();

        var paths = new WorkshopGridPaths(_grid, ClearanceCells);
        var doorway = paths.CellOf(_grid.GetNode<Node3D>("Doorway").GlobalPosition);
        var reachable = paths.ReachableFrom(doorway);
        var rooms = _grid.GetNode("Rooms").GetChildren().OfType<Marker3D>().ToList();

        AssertBool(rooms.Count >= 5).IsTrue();
        AssertThat(reachable.Count).IsEqual(paths.Clear.Count);
        AssertThat(paths.CountFloorCellsTooTight(ClearanceCells)).IsEqual(0);
        foreach (var room in rooms)
            AssertBool(reachable.Contains(paths.CellOf(room.GlobalPosition))).IsTrue();

        var farthest = paths.CellOf(_grid.GetNode<Node3D>("Rooms/FarNorth").GlobalPosition);
        AssertBool(paths.StraightenPath(paths.ShortestPath(doorway, farthest)).Count - 1 >= 4).IsTrue();
        AssertThat(_grid.MeshLibrary.GetItemShapes(WorkshopGridPaths.FloorItem).Count).IsEqual(2);
        AssertThat(_grid.MeshLibrary.GetItemShapes(WorkshopGridPaths.WallItem).Count).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerWalksFromWorkshopEntryThroughEveryRoomWithoutFalling()
    {
        Engine.TimeScale = 4f;
        _player.GlobalPosition = _workshop.GetNode<Node3D>("WorkshopEntry").GlobalPosition;
        await Settle();

        var paths = new WorkshopGridPaths(_grid, ClearanceCells);
        var here = paths.CellOf(_grid.GetNode<Node3D>("Doorway").GlobalPosition);
        await WalkAndAssert(paths.WorldXz(here));

        foreach (var room in _grid.GetNode("Rooms").GetChildren().OfType<Marker3D>()
                     .OrderBy(room => room.GlobalPosition.DistanceTo(_player.GlobalPosition)))
        {
            var target = paths.CellOf(room.GlobalPosition);
            var route = paths.ShortestPath(here, target);
            AssertThat(route.Count).IsGreater(0);
            foreach (var waypoint in paths.StraightenPath(route))
                await WalkAndAssert(paths.WorldXz(waypoint));
            here = target;
        }

        AssertBool(_highest - _lowest < MaxHeightVariation).IsTrue();
        AssertBool(_airborneFrames <= _frames * 0.01f).IsTrue();
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
}
