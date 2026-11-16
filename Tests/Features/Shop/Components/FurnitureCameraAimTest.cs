using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The camera paths of the placement controller, run for real: a current Camera3D aims the ghost through
///     <c>_PhysicsProcess</c>, and the G key picks up the placed piece the camera's ray hits. No aim override is set.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class FurnitureCameraAimTest
{
    private const string BoxScenePath = "res://Scenes/Shop/Items/ShelfBox.tscn";
    private const string BoxCatalogPath = "res://Data/Shop/FurnitureOrderCatalog.tres";
    private const float CameraHeight = 3f;

    private InputService _input = null!;
    private Node3D _root = null!;
    private Node3D _world = null!;
    private Camera3D _camera = null!;
    private FurniturePlacementController _controller = null!;

    [BeforeTest]
    public void Setup()
    {
        TestServices.Reset();
        _input = new InputService();
        TestServices.Container.RegisterSingleton<IInputService>(_input);
        _root = new Node3D { Name = "Furniture" };
        _world = new Node3D { Name = "World" };
        _camera = new Camera3D();
        _controller = TestServices.Inject(new FurniturePlacementController { FurnitureRoot = _root });
        AddNode(_root);
        AddNode(_world);
        AddNode(_input);
        AddNode(_camera);
        AddNode(_controller);
        TestServices.ExecutePendingCallbacks();
        _camera.Current = true;
    }

    [AfterTest]
    public void TearDown()
    {
        _controller.Cancel();
        TestServices.Reset();
        _controller = null!;
        _camera = null!;
        _world = null!;
        _root = null!;
        _input = null!;
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheCameraRayMovesTheGhostToTheFloorPointItMeets()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();

        await ISceneRunner.SyncPhysicsFrame;
        Aim(new Vector3(8f, CameraHeight, 9f), new Vector3(8f, 0f, 6f));
        _controller._PhysicsProcess(0.0);
        AssertBool(_controller.CurrentPose.X == 8f && _controller.CurrentPose.Z == 6f).IsTrue();

        Aim(new Vector3(4f, CameraHeight, 9f), new Vector3(4f, 0f, 6f));
        _controller._PhysicsProcess(0.0);
        AssertBool(_controller.CurrentPose.X == 4f && _controller.CurrentPose.Z == 6f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACameraThatLooksAtTheHorizonLeavesThePoseAlone()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        var start = _controller.CurrentPose;

        await ISceneRunner.SyncPhysicsFrame;
        Aim(new Vector3(8f, 1.5f, 9f), new Vector3(8f, 1.5f, 3f));
        _controller._PhysicsProcess(0.0);

        AssertBool(_controller.CurrentPose == start).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ThePickUpKeyMovesThePieceTheCameraRayHits()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        _controller.AimAt(8f, 6f);
        AssertBool(_controller.Confirm()).IsTrue();
        var placed = (PlacedFurniture)_root.GetChild(0);
        await ISceneRunner.SyncPhysicsFrame;

        Aim(new Vector3(8f, 1.5f, 9f), new Vector3(8f, 0.5f, 6f));
        _controller._PhysicsProcess(0.0);
        AssertBool(ReferenceEquals(_controller.AimedFurniture, placed)).IsTrue();

        Press(Key.G);

        AssertBool(_controller.IsActive).IsTrue();
        AssertBool(placed.IsCarried).IsTrue();
        AssertInt(_root.GetChildCount()).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ACameraOnThePlayersOwnBodyStillAimsAtThePiece()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        _controller.AimAt(8f, 6f);
        AssertBool(_controller.Confirm()).IsTrue();
        var placed = (PlacedFurniture)_root.GetChild(0);

        // As in player.tscn: a 1.8 m capsule on layer 1 (the furniture layer) with the camera at its top, so the
        // ray starts on the player's own body.
        var player = new CharacterBody3D { CollisionLayer = 1, Position = new Vector3(8f, 0.9f, 9f) };
        player.AddChild(new CollisionShape3D { Shape = new CapsuleShape3D { Height = 1.8f } });
        var eye = new Camera3D { Position = new Vector3(0f, 0.9f, 0f) };
        player.AddChild(eye);
        AddNode(player);
        eye.Current = true;
        eye.LookAt(new Vector3(8f, 0.5f, 6f), Vector3.Up);
        await ISceneRunner.SyncPhysicsFrame;

        _controller._PhysicsProcess(0.0);

        AssertBool(ReferenceEquals(_controller.AimedFurniture, placed)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task ThePackKeyPacksThePieceTheCameraRayHitsIntoItsBox()
    {
        _controller.Boxes = GD.Load<OrderCatalog>(BoxCatalogPath);
        _controller.BoxRoot = _world;
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        _controller.AimAt(8f, 6f);
        AssertBool(_controller.Confirm()).IsTrue();
        await ISceneRunner.SyncPhysicsFrame;

        Aim(new Vector3(8f, 1.5f, 9f), new Vector3(8f, 0.5f, 6f));
        _controller._PhysicsProcess(0.0);
        Press(Key.B);

        AssertInt(_root.GetChildCount()).IsEqual(0);
        AssertBool(_controller.AimedFurniture == null).IsTrue();
        var box = _world.GetChildren().OfType<FurnitureBox>().Single(candidate => !candidate.IsQueuedForDeletion());
        AssertThat(box.Item!.Id).IsEqual("shelf_unit");
        AssertBool(box.GlobalPosition.X == 8f && box.GlobalPosition.Z == 6f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TheCameraRayMissingEveryPieceLeavesNoPickUpTarget()
    {
        AssertBool(_controller.Begin(NewBox())).IsTrue();
        _controller.AimAt(8f, 6f);
        AssertBool(_controller.Confirm()).IsTrue();
        await ISceneRunner.SyncPhysicsFrame;

        Aim(new Vector3(2f, 1.5f, 9f), new Vector3(2f, 0.5f, 6f));
        _controller._PhysicsProcess(0.0);
        Press(Key.G);

        AssertBool(_controller.AimedFurniture == null).IsTrue();
        AssertBool(_controller.IsActive).IsFalse();
    }

    private void Aim(Vector3 from, Vector3 at)
    {
        _camera.GlobalPosition = from;
        _camera.LookAt(at, Vector3.Up);
    }

    /// <summary>Instances a shelf box, injects its services and adds it under the World node.</summary>
    private FurnitureBox NewBox()
    {
        var box = GD.Load<PackedScene>(BoxScenePath).Instantiate<FurnitureBox>();
        TestServices.Inject(box);
        _world.AddChild(box);
        return box;
    }

    private void Press(Key key) =>
        _input._Input(new InputEventKey { Keycode = key, Pressed = true });
}
