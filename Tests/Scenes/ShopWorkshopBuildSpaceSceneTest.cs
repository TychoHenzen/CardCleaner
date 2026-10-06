using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Workshop.Components;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The warehouse workshop sits far from the shop without overlapping it, the build rooms lie to the right of
/// the cabinet and the ordering rooms to its left, and the production player can walk from the portal entry
/// through every room and hallway around the cabinet and the terminal.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopBuildSpaceSceneTest
{
    private const float PlayerStartHeight = 1.2f;
    private const int ClearanceCells = 4;
    private const double WalkTimeScale = 4.0;
    private const float MaxHeightVariation = 0.05f;
    private const float MaxAirborneShare = 0.01f;
    private const float FallLimitY = -1f;
    private const float MinimumDistanceFromShop = 30f;

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
    public void BuildRoomsLieRightOfTheCabinetAndOrderingRoomsLeftOfIt()
    {
        var cabinet = _workshop.GetNode<Node3D>("Cabinet").GlobalPosition;

        foreach (var name in new[] { "BuildRoomA", "BuildRoomB", "BuildHallC", "EastHall" })
            AssertBool(Section(name).X > cabinet.X).IsTrue();
        foreach (var name in new[] { "OrderOffice", "DeliveryBay" })
            AssertBool(Section(name).X < cabinet.X).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopIsFarFromTheShopAndOverlapsNoShopGeometry()
    {
        var floor = _grid.GetNode<MeshInstance3D>("Floor/Mesh");
        var ceiling = _grid.GetNode<MeshInstance3D>("Ceiling/Mesh");
        var bounds = WorldBounds(floor).Merge(WorldBounds(ceiling));
        var shopMeshes = _shop.FindChildren("*", nameof(MeshInstance3D), true, false)
            .OfType<MeshInstance3D>()
            .Where(m => !_workshop.IsAncestorOf(m) && m.Mesh != null);

        AssertBool(bounds.Position.X - _player.GlobalPosition.X > MinimumDistanceFromShop).IsTrue();
        foreach (var mesh in shopMeshes)
            AssertBool(bounds.Intersects(WorldBounds(mesh))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EverySectionIsReachableFromTheEntryAroundTheProps()
    {
        var paths = new WorkshopGridPaths(_grid, ClearanceCells, WorkshopGridPaths.SolidProps(_workshop, _grid));
        var reachable = paths.ReachableFrom(paths.CellOf(_workshop.GetNode<Node3D>("WorkshopEntry").GlobalPosition));

        foreach (var marker in Sections())
            AssertBool(reachable.Contains(paths.CellOf(marker.GlobalPosition))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task PlayerWalksFromTheEntryThroughEverySectionWithoutFalling()
    {
        // Same fixed physics step, more steps per real second: the long route would otherwise run in real time.
        Engine.TimeScale = WalkTimeScale;
        var entry = _workshop.GetNode<Node3D>("WorkshopEntry").GlobalPosition;
        _player.GlobalPosition = new Vector3(entry.X, PlayerStartHeight, entry.Z);
        await Settle();

        var paths = new WorkshopGridPaths(_grid, ClearanceCells, WorkshopGridPaths.SolidProps(_workshop, _grid));
        var here = paths.CellOf(_player.GlobalPosition);
        var pending = Sections().ToList();
        while (pending.Count > 0)
        {
            var next = pending.OrderBy(m => paths.CellOf(m.GlobalPosition).DistanceSquaredTo(here)).First();
            pending.Remove(next);
            var target = paths.CellOf(next.GlobalPosition);
            var route = paths.ShortestPath(here, target);
            AssertThat(route.Count).IsGreater(0);
            foreach (var waypoint in paths.StraightenPath(route))
                await WalkAndAssert(paths.WorldXz(waypoint));
            here = target;
        }

        GD.Print($"[workshop-walk] frames {_frames}, airborne {_airborneFrames}, y {_lowest:F3}..{_highest:F3}");
        AssertBool(_highest - _lowest < MaxHeightVariation).IsTrue();
        AssertBool(_airborneFrames <= _frames * MaxAirborneShare).IsTrue();
    }

    private Vector3 Section(string name) => _grid.GetNode<Node3D>($"Sections/{name}").GlobalPosition;

    private System.Collections.Generic.IEnumerable<Marker3D> Sections() =>
        _grid.GetNode("Sections").GetChildren().OfType<Marker3D>();

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
}
