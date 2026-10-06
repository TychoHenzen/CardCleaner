using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// End to end in the shop scene: the workshop ordering room sits beside the cabinet, its terminal is reachable
/// by the player's interaction ray, opens its own catalog UI, and ordering charges the shared balance and
/// delivers a physical item to the workshop delivery marker rather than the storage room's.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopOrderingSceneTest
{
    private const float StandingDistanceToTerminal = 2.0f;
    private static readonly string[] ExpectedItems = ["conveyor", "arcade_cabinet", "wiring"];

    private Node3D _shop = null!;
    private Node3D _workshop = null!;
    private PlayerController _player = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _ui = null!;
    private MoneyService _money = null!;
    private Marker3D _delivery = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _workshop = _shop.GetNode<Node3D>("World/Workshop");
        _player = _shop.GetNode<PlayerController>("Player");
        _terminal = _workshop.GetNode<OrderTerminal>("OrderingRoom/OrderTerminal");
        _ui = _workshop.GetNode<OrderTerminalUi>("OrderUi");
        _delivery = _workshop.GetNode<Marker3D>("DeliveryPoint");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public static void Teardown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalListsConveyorArcadeCabinetAndWiringWithPrices()
    {
        AssertThat(_terminal.Catalog!.Items.Select(i => i.Id).ToArray()).IsEqual(ExpectedItems);
        AssertThat(_ui.ItemRowCount).IsEqual(ExpectedItems.Length);
        AssertBool(_terminal.Catalog.Items.All(i => i.Price > 0)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopTerminalIsADifferentNodeWithItsOwnUiFromTheShopPc()
    {
        var pc = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");
        var pcUi = _shop.GetNode<OrderTerminalUi>("OrderUi");

        AssertBool(ReferenceEquals(pc, _terminal)).IsFalse();
        AssertBool(ReferenceEquals(pcUi, _ui)).IsFalse();
        AssertBool(ReferenceEquals(_terminal.Ui, _ui)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalSitsInARoomLeftOfTheCabinetInsideTheWorkshopWalls()
    {
        var cabinet = _workshop.GetNode<StaticBody3D>("Cabinet");
        var floor = (BoxShape3D)_workshop.GetNode<CollisionShape3D>("Room/Floor/CollisionShape3D").Shape;
        var terminal = _terminal.GlobalPosition - _workshop.GlobalPosition;

        AssertBool(_terminal.GlobalPosition.X < cabinet.GlobalPosition.X).IsTrue();
        AssertBool(Mathf.Abs(terminal.X) < floor.Size.X / 2f).IsTrue();
        AssertBool(Mathf.Abs(terminal.Z) < floor.Size.Z / 2f).IsTrue();
        AssertBool(_workshop.GetNode("OrderingRoom").GetChildren().OfType<StaticBody3D>().Count() >= 3).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerInteractionRayReachesTheTerminalFromInsideTheRoom()
    {
        var interaction = _player.GetNode<InteractionSystem>("Head/Camera3D/InteractionSystem");
        var from = _terminal.GlobalPosition + new Vector3(0f, 1.0f, StandingDistanceToTerminal);
        var to = _terminal.GlobalPosition + new Vector3(0f, 1.0f, 0f);

        var hit = _shop.GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from, To = to, CollideWithBodies = true, CollisionMask = interaction.InteractableCollisionMask
        });

        AssertThat(hit.Count).IsGreater(0);
        AssertThat(hit["collider"].Obj).IsEqual(_terminal);
        AssertBool(from.DistanceTo(_terminal.GlobalPosition) <= _terminal.InteractionRange).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PlayerCanWalkFromTheWorkshopEntryThroughTheRoomDoorToTheTerminal()
    {
        var shape = (CapsuleShape3D)_player.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var probe = new ShopSceneProbe(_shop, shape);
        var entry = _workshop.GetNode<Marker3D>("WorkshopEntry").GlobalPosition;
        var door = _workshop.GlobalPosition + new Vector3(-4.3f, 0f, -2.0f);
        var inside = _terminal.GlobalPosition + new Vector3(0f, 0f, StandingDistanceToTerminal);
        Vector2[] route =
        [
            new(entry.X, entry.Z), new(door.X, door.Z + 1.5f), new(door.X, door.Z), new(inside.X, inside.Z)
        ];

        AssertBool(probe.CanWalk(route)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalOpensItsOwnUiWithTheSharedBalance()
    {
        _terminal.Interact();

        AssertBool(_ui.Visible).IsTrue();
        AssertBool(_shop.GetNode<OrderTerminalUi>("OrderUi").Visible).IsFalse();
        AssertThat(_ui.BalanceText).IsEqual($"Balance: {_money.Balance}");
        AssertBool(_player.ControlEnabled).IsFalse();

        _terminal.Close();

        AssertBool(_player.ControlEnabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingEachItemChargesThePriceAndDeliversItToTheWorkshopMarker()
    {
        var world = _shop.GetNode("World");
        _terminal.Interact();

        for (var i = 0; i < ExpectedItems.Length; i++)
        {
            var item = _terminal.Catalog!.Items[i];
            _money.Add(item.Price);
            var balance = _money.Balance;
            var before = world.GetChildCount();

            _ui.PressItem(i);

            AssertThat(_money.Balance).IsEqual(balance - item.Price);
            AssertThat(world.GetChildCount()).IsEqual(before + 1);
            var spawned = (Node3D)world.GetChildren().Last();
            AssertBool(spawned.GlobalPosition.IsEqualApprox(
                _delivery.GlobalPosition + OrderingService.SlotOffset(i))).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RoomIsClosedOnTheHallSideExceptForTheDoorGap()
    {
        var space = _shop.GetWorld3D().DirectSpaceState;
        var inside = _workshop.GlobalPosition + new Vector3(-4.5f, 1.0f, -3.5f);
        var hall = _workshop.GlobalPosition + new Vector3(-2.0f, 1.0f, -3.5f);
        var doorSide = _workshop.GlobalPosition + new Vector3(-4.3f, 1.0f, -1.0f);

        var throughWall = space.IntersectRay(PhysicsRayQueryParameters3D.Create(inside, hall));
        var throughDoor = space.IntersectRay(PhysicsRayQueryParameters3D.Create(
            _workshop.GlobalPosition + new Vector3(-4.3f, 1.0f, -3.5f), doorSide));

        AssertThat(throughWall.Count).IsGreater(0);
        AssertThat(throughDoor.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TerminalArtSlotsUseManifestPackMeshesWithAPlaceholderFallback()
    {
        var slots = _terminal.GetChildren().OfType<ShopArtSlot>().ToList();

        AssertThat(slots.Count).IsEqual(3);
        foreach (var slot in slots)
        {
            AssertBool(slot.ArtPath.StartsWith("res://Assets/Synty/")).IsTrue();
            AssertBool(SyntyManifest.ListsTarget(slot.ArtPath["res://Assets/Synty/".Length..])).IsTrue();
            AssertThat(slot.Placeholder).IsNotNull();
            AssertBool(slot.ArtLoaded != slot.Placeholder!.Visible).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderedConveyorIsAPhysicalBodyWithCollisionAndKeepsItsConveyorBehaviourNode()
    {
        _terminal.Interact();
        _money.Add(1000);

        _ui.PressItem(0);

        var spawned = (Node3D)_shop.GetNode("World").GetChildren().Last();
        var belt = spawned.GetNodeOrNull("Belt");
        AssertThat(belt).IsNotNull();
        AssertBool(belt!.IsClass("StaticBody3D")).IsTrue();
        AssertThat(belt.GetNodeOrNull<CollisionShape3D>("CollisionShape3D")).IsNotNull();
    }
}
