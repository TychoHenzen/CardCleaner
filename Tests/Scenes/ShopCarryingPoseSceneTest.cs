using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Data;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Tests.TestUtilities;
using Godot;
using NSubstitute;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// The shop player carries an ordered container in the hand: the held box sits in front of the camera, and
/// the walk, drop, pick-up and open loop keeps the box, its physics and its packs consistent.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCarryingPoseSceneTest
{
    private const ulong Seed = 4242;
    private const float PlaceDistance = 1.5f;
    private const float WalkDistance = 2f;
    private const float DropTolerance = 0.3f;
    private const float FallLimitY = -0.5f;
    private const int OpenFrames = 12;
    private const int SettleFrames = 60;
    private const int InteractFrames = 6;
    private const int CarryFrames = 10;

    // The right storage arm of the shop scene, where the delivery point is, inside its walls.
    private static readonly Rect2 StorageRoom = new(16f, -8f, 8f, 20f);

    private Node3D _shop = null!;
    private Node3D _world = null!;
    private PlayerController _player = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private Vector2I _savedRootSize;

    [BeforeTest]
    public async Task Setup()
    {
        // A headless window is 64x64, which would collapse the camera frustum, so the root takes a real size.
        var root = ((SceneTree)Engine.GetMainLoop()).Root;
        _savedRootSize = root.Size;
        root.Size = new Vector2I(1280, 720);

        _shop = GD.Load<PackedScene>(ShopSceneProbe.ScenePath).Instantiate<Node3D>();
        _world = _shop.GetNode<Node3D>("World");
        _player = _shop.GetNode<PlayerController>("Player");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

        // The test scene is not the current scene, so hand the scene-owned services over directly.
        TestServices.Reset();
        TestServices.Container.RegisterSingleton<IGameSettings>(_shop.GetNode<GameSettings>("Services/GameSettings"));
        TestServices.Container.RegisterSingleton<IMoneyService>(_money);
        TestServices.Container.RegisterSingleton<IOrderingService>(_ordering);
        TestServices.Container.RegisterSingleton(RandomSources.Create(Seed));
        TestServices.Container.RegisterSingleton(Substitute.For<ICardGenerator>());
        TestServices.Container.RegisterSingleton<ICardSpawningService>(
            _shop.GetNode<CardSpawningService>("Services/SpawningService"));
        TestServices.Container.RegisterSingleton<ICardSpawner>((ICardSpawner)_shop.GetNode("World/Cards"));

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;

        // The shop scene has no input node of its own, so the test adds the real one and resolves the
        // callbacks that InteractionSystem, CardHighlighter and ContainerHolder queued while waiting for it.
        var input = AddNode(new InputService());
        TestServices.Container.RegisterSingleton<IInputService>(input);
        TestServices.ExecutePendingCallbacks();
    }

    [AfterTest]
    public void Teardown()
    {
        Input.ActionRelease("ui_up");
        ((SceneTree)Engine.GetMainLoop()).Root.Size = _savedRootSize;
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task HeldBoxSitsInFrontOfTheCamera()
    {
        var box = OrderBox();
        await PlaceFacing(box);
        await ClickAsync(MouseButton.Left);
        await PhysicsFrames(2);

        var camera = Camera();
        var holder = Holder();
        AssertBool(ReferenceEquals(holder.Held, box))
            .OverrideFailureMessage(
                $"nothing picked up: camera y={camera.GlobalPosition.Y}, box top y={box.GlobalPosition.Y + 0.35f}, " +
                $"Held={holder.Held?.Name ?? "null"}")
            .IsTrue();
        AssertBool(ReferenceEquals(box.GetParent(), camera)).IsTrue();
        AssertBool(box.Transform.Origin.IsEqualApprox(holder.HoldOffset)).IsTrue();
        AssertBool(camera.IsPositionInFrustum(box.GlobalPosition)).IsTrue();

        var shape = box.GetChildren().OfType<CollisionShape3D>().First();
        var half = ((BoxShape3D)shape.Shape).Size / 2f;
        var forward = -camera.GlobalBasis.Z;
        var inFrustum = 0;
        for (var sx = -1; sx <= 1; sx += 2)
        for (var sy = -1; sy <= 1; sy += 2)
        for (var sz = -1; sz <= 1; sz += 2)
        {
            var corner = shape.GlobalTransform * new Vector3(sx * half.X, sy * half.Y, sz * half.Z);
            AssertBool((corner - camera.GlobalPosition).Dot(forward) > camera.Near).IsTrue();
            if (camera.IsPositionInFrustum(corner))
                inFrustum++;
        }

        GD.Print($"HeldBoxSitsInFrontOfTheCamera: {inFrustum} of 8 corners inside the frustum");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CarryLoopWorksThroughAWalkDropPickUpAndOpen()
    {
        var box = OrderBox();
        // Recorded before the first click: the pickup clears the layers and the drop must restore these.
        var layer = box.CollisionLayer;
        var mask = box.CollisionMask;
        await PlaceFacing(box);
        await ClickAsync(MouseButton.Left);
        await PhysicsFrames(2);
        var holder = Holder();
        AssertBool(ReferenceEquals(holder.Held, box)).IsTrue();

        var carriedTransform = box.Transform;
        var start = _player.GlobalPosition;
        var startXz = new Vector2(start.X, start.Z);
        var target = startXz + (StorageRoom.GetCenter() - startXz).Normalized() * WalkDistance;
        AssertBool(StorageRoom.HasPoint(target)).IsTrue();

        await PlayerWalker.WalkTo(_player, target);
        await PhysicsFrames(CarryFrames);

        AssertBool(ReferenceEquals(holder.Held, box)).IsTrue();
        AssertBool(box.Transform.IsEqualApprox(carriedTransform)).IsTrue();
        var walked = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z).DistanceTo(startXz);
        AssertBool(walked >= 1.5f).OverrideFailureMessage($"player walked only {walked} m").IsTrue();

        await ClickAsync(MouseButton.Right);

        var camera = Camera();
        var expected = camera.GlobalPosition + (-camera.GlobalBasis.Z * holder.DropDistance);
        AssertBool(holder.Held == null).IsTrue();
        AssertBool(ReferenceEquals(box.GetParent(), SpawnerNode())).IsTrue();
        AssertBool(box.CollisionLayer == layer && box.CollisionMask == mask).IsTrue();
        AssertBool(!box.Freeze).IsTrue();
        AssertBool(box.GlobalPosition.DistanceTo(expected) <= DropTolerance)
            .OverrideFailureMessage($"box dropped at {box.GlobalPosition}, expected {expected}")
            .IsTrue();

        await PhysicsFrames(SettleFrames);
        AssertBool(box.GlobalPosition.Y > FallLimitY)
            .OverrideFailureMessage($"box fell to y={box.GlobalPosition.Y}")
            .IsTrue();

        await FaceBox(box);
        await ClickAsync(MouseButton.Left);
        await PhysicsFrames(2);
        AssertBool(ReferenceEquals(holder.Held, box))
            .OverrideFailureMessage(Describe(box))
            .IsTrue();

        await PressKeyAsync(Key.Q);
        await PhysicsFrames(OpenFrames);

        AssertBool(box.IsOpened).IsTrue();
        AssertBool(holder.Held == null).IsTrue();
        var packs = SpawnerNode().GetChildren().OfType<CardContainer>()
            .Where(c => c.Kind == CardContainerKind.Pack && !c.IsOpened).ToArray();
        AssertThat(packs.Length).IsEqual(CardContainerLayout.ItemsPerContainer);
    }

    // What the interaction ray sees, for a failure message when a click does not pick the box up.
    private string Describe(CardContainer box)
    {
        var camera = Camera();
        var origin = camera.GlobalPosition;
        var hit = _player.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = origin,
            To = origin + (-camera.GlobalBasis.Z * 100f),
            CollideWithBodies = true,
            CollisionMask = 6
        });
        var collider = hit.Count > 0 ? ((Node)hit["collider"].Obj!).Name.ToString() : "none";
        return $"camera={origin} box={box.GlobalPosition} layer={box.CollisionLayer} canInteract={box.CanInteract} " +
               $"held={box.IsHeld} parent={box.GetParent()?.Name} ray={collider} player={_player.GlobalPosition}";
    }

    private CardContainer OrderBox()
    {
        var item = Catalog().Items.Single(i => i.Id == "cardboard_box");
        _money.Add(item.Price);
        var result = _ordering.Order(item);
        AssertBool(result.Succeeded).IsTrue();
        return (CardContainer)result.Spawned!;
    }

    private OrderCatalog Catalog() =>
        _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal").Catalog!;

    private ContainerHolder Holder() => _player.GetNode<ContainerHolder>("CardInteraction/ContainerHolder");

    private Node3D SpawnerNode() => TestServices.Resolver.Get<ICardSpawner>().GetNode();

    private Camera3D Camera() => _player.GetNode<Camera3D>("Head/Camera3D");

    // Stands the player PlaceDistance in front of the box, on the storage-room side, facing the box.
    private async Task PlaceFacing(CardContainer box)
    {
        var boxXz = new Vector2(box.GlobalPosition.X, box.GlobalPosition.Z);
        var towardRoom = (StorageRoom.GetCenter() - boxXz).Normalized();
        var standing = boxXz + towardRoom * PlaceDistance;
        _player.GlobalPosition = new Vector3(standing.X, _player.GlobalPosition.Y, standing.Y);
        await FaceBox(box);
    }

    // Turns the player and the camera at the box from where the player stands, without moving the player. A
    // teleport next to a settled box makes the physics shove it away, so the second pick-up only turns.
    private async Task FaceBox(CardContainer box)
    {
        var boxXz = new Vector2(box.GlobalPosition.X, box.GlobalPosition.Z);
        var standingXz = new Vector2(_player.GlobalPosition.X, _player.GlobalPosition.Z);

        // "ui_up" moves along local -Z, so yaw the body until -Z points at the box (the same rule as PlayerWalker).
        var offset = boxXz - standingXz;
        _player.Rotation = new Vector3(0f, Mathf.Atan2(-offset.X, -offset.Y), 0f);

        // The yaw alone leaves the camera level, above the box, so the ray misses it. Pitch the camera at the box the
        // way ShopCarryingSceneTest.Aim does.
        Camera().LookAt(box.GlobalPosition + new Vector3(0f, 0.2f, 0f));
        await PhysicsFrames(InteractFrames);
    }

    // A press and a release through the input pipeline, with a physics frame between them so the press
    // is not held down into the next click.
    private static async Task ClickAsync(MouseButton button)
    {
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = true });
        await ISceneRunner.SyncPhysicsFrame;
        Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = button, Pressed = false });
    }

    private static async Task PressKeyAsync(Key key)
    {
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = true });
        await ISceneRunner.SyncPhysicsFrame;
        Input.ParseInputEvent(new InputEventKey { Keycode = key, Pressed = false });
    }

    private static async Task PhysicsFrames(int count)
    {
        for (var i = 0; i < count; i++)
            await ISceneRunner.SyncPhysicsFrame;
    }
}
