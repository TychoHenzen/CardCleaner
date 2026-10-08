using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Reliability of the workshop terminal inside the shop scene: its deliveries never overlap workshop geometry,
/// use a single ground layer so tall items never reach the ceiling, leave the shop delivery grid alone, and the balance
/// stays consistent when the two terminals order alternately.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopDeliverySceneTest
{
    private const float WidestItem = 1.3f;
    private const float TallestItem = 2.24f;
    private const float ProbeInset = 0.02f;
    private const int Rounds = 3;
    private const int TopUp = 10000;
    private const int SettleFrames = 300;
    private const float UprightDot = 0.95f;
    private const float PadReach = 3.5f;
    private const float RestTolerance = 0.1f;

    private Node3D _shop = null!;
    private Node3D _workshop = null!;
    private OrderTerminal _pc = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _pcUi = null!;
    private OrderTerminalUi _ui = null!;
    private MoneyService _money = null!;
    private OrderingService _ordering = null!;
    private DeliveryMarker _delivery = null!;
    private Marker3D _shopDelivery = null!;

    private static int Capacity => OrderingService.SlotColumns * OrderingService.SlotRows;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _workshop = _shop.GetNode<Node3D>("World/Workshop");
        _pc = _shop.GetNode<OrderTerminal>("World/Markers/PcLocation/PcTerminal");
        _terminal = _workshop.GetNode<OrderTerminal>("OrderingRoom/OrderTerminal");
        _pcUi = _shop.GetNode<OrderTerminalUi>("OrderUi");
        _ui = _workshop.GetNode<OrderTerminalUi>("OrderUi");
        _delivery = _workshop.GetNode<DeliveryMarker>("DeliveryPoint");
        _shopDelivery = _shop.GetNode<Marker3D>("World/Markers/DeliveryPoint");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _ordering = _shop.GetNode<OrderingService>("Services/OrderingService");

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
    public void TerminalDeliversToItsOwnMarkerNotTheShopDefault()
    {
        AssertBool(ReferenceEquals(_terminal.DeliveryPoint, _delivery)).IsTrue();
        AssertBool(_delivery.GlobalPosition.DistanceTo(_shopDelivery.GlobalPosition) > 20f).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EveryDeliverySlotIsFreeOfWorkshopGeometryForTheTallestItem()
    {
        var space = _shop.GetWorld3D().DirectSpaceState;
        var box = new BoxShape3D { Size = new Vector3(WidestItem, TallestItem - ProbeInset * 2f, WidestItem) };

        for (var slot = 0; slot < Capacity; slot++)
        {
            var feet = _delivery.GlobalPosition + OrderingService.SlotOffset(slot);
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = box,
                Transform = new Transform3D(Basis.Identity, feet + Vector3.Up * (TallestItem / 2f)),
                CollideWithBodies = true,
                CollideWithAreas = false
            };

            AssertThat(space.IntersectShape(query, 1).Count).IsEqual(0);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TallestItemInTheSingleLayerClearsTheCeiling()
    {
        var ceiling = _workshop.GetNode<StaticBody3D>("WorkshopGrid/Ceiling");
        var shape = (BoxShape3D)ceiling.GetNode<CollisionShape3D>("CollisionShape3D").Shape;
        var underside = ceiling.GlobalPosition.Y - shape.Size.Y / 2f;

        AssertBool(_delivery.GlobalPosition.Y + TallestItem < underside).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemovingADeliveredItemFreesItsSlotForTheNextOrder()
    {
        _money.Add(TopUp);
        _terminal.Interact();
        var cabinet = IndexOf("arcade_cabinet");
        var first = PressForDelivery(_terminal, cabinet);
        OrderUntilFull(_terminal, cabinet);

        first.QueueFree();
        var replacement = PressForDelivery(_terminal, cabinet);

        AssertBool(replacement.GlobalPosition.IsEqualApprox(_delivery.GlobalPosition + OrderingService.SlotOffset(0)))
            .IsTrue();
        AssertThat(_ui.MessageText.Contains("Ordered")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FullDeliveryAreaRejectsTheNextTallOrderWithAMessageAndNoCharge()
    {
        _money.Add(TopUp);
        _terminal.Interact();
        var cabinet = IndexOf("arcade_cabinet");
        OrderUntilFull(_terminal, cabinet);
        var balance = _money.Balance;
        var spawned = _shop.GetNode("World").GetChildCount();

        PressForRefusal(_terminal, cabinet, OrderStatus.DeliveryFull);

        AssertThat(_money.Balance).IsEqual(balance);
        AssertThat(_shop.GetNode("World").GetChildCount()).IsEqual(spawned);
        AssertBool(_ui.MessageText.StartsWith("The delivery area is full")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingNeverLocksAfterManyDeliveriesOfSmallItems()
    {
        _money.Add(TopUp);
        _terminal.Interact();
        var wiring = IndexOf("wiring");
        var ceilingClearance = _delivery.GlobalPosition.Y + _delivery.MaxStackHeight;

        for (var order = 0; order < 4 * Capacity; order++)
        {
            var delivered = PressForDelivery(_terminal, wiring);

            AssertBool(_ui.MessageText.Contains("Ordered")).IsTrue();
            AssertBool(delivered.GlobalPosition.Y < ceilingClearance).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopOrdersLeaveTheShopDeliveryGridUntouched()
    {
        _money.Add(TopUp);
        _terminal.Interact();
        PressForDelivery(_terminal, IndexOf("wiring"));
        PressForDelivery(_terminal, IndexOf("wiring"));
        _terminal.Close();

        _pc.Interact();
        var spawned = PressForDelivery(_pc, 0);
        AssertBool(spawned.GlobalPosition.IsEqualApprox(_shopDelivery.GlobalPosition + OrderingService.SlotOffset(0)))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AlternatingOrdersFromBothTerminalsKeepOneConsistentBalanceAndTwoSeparateGrids()
    {
        _money.Add(TopUp);
        var expected = _money.Balance;
        var wiringIndex = IndexOf("wiring");
        var wiringPrice = _terminal.Catalog!.Items[wiringIndex].Price;
        var boxIndex = _pc.Catalog!.Items.ToList().FindIndex(i => i.Id == "cardboard_box");
        var boxPrice = _pc.Catalog.Items[boxIndex].Price;

        for (var round = 0; round < Rounds; round++)
        {
            _terminal.Interact();
            var workshopItem = PressForDelivery(_terminal, wiringIndex);
            _terminal.Close();
            expected -= wiringPrice;
            AssertThat(_money.Balance).IsEqual(expected);
            AssertBool(workshopItem.GlobalPosition.IsEqualApprox(
                _delivery.GlobalPosition + OrderingService.SlotOffset(round))).IsTrue();

            _pc.Interact();
            var shopItem = PressForDelivery(_pc, boxIndex);
            _pc.Close();
            expected -= boxPrice;
            AssertThat(_money.Balance).IsEqual(expected);
            AssertBool(shopItem.GlobalPosition.IsEqualApprox(
                _shopDelivery.GlobalPosition + OrderingService.SlotOffset(round))).IsTrue();

            AssertThat(_pcUi.BalanceText).IsEqual($"Balance: {expected}");
            AssertThat(_ui.BalanceText).IsEqual($"Balance: {expected}");
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task OrderedCabinetSettlesUprightOnThePadWithItsHolders()
    {
        _money.Add(TopUp);
        _terminal.Interact();
        var cabinet = (RigidBody3D)PressForDelivery(_terminal, IndexOf("arcade_cabinet"));
        _terminal.Close();
        for (var i = 0; i < SettleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        var offset = cabinet.GlobalPosition - _delivery.GlobalPosition;
        AssertBool(cabinet.GlobalBasis.Y.Dot(Vector3.Up) > UprightDot)
            .OverrideFailureMessage($"cabinet tipped: up {cabinet.GlobalBasis.Y}")
            .IsTrue();
        AssertBool(new Vector2(offset.X, offset.Z).Length() < PadReach && Mathf.Abs(offset.Y) < RestTolerance)
            .OverrideFailureMessage($"cabinet came to rest at {offset} from the pad")
            .IsTrue();
        AssertThat(cabinet.GetNodeOrNull("Assembly/DeckSlot")).IsNotNull();
        AssertThat(cabinet.GetNodeOrNull("Assembly/CardSlot")).IsNotNull();
    }

    private int IndexOf(string id) => _terminal.Catalog!.Items.ToList().FindIndex(i => i.Id == id);

    /// <summary>
    /// Presses an item and reports what it did. The message is cleared first: LastOrder is reset only inside the
    /// terminal's order handler, so a press that never arrives would otherwise leave the previous result in place.
    /// </summary>
    private PressResult Press(OrderTerminal terminal, int index)
    {
        var ui = terminal.Ui!;
        var before = _ordering.DeliveredCount;
        ui.ShowMessage(string.Empty);
        ui.PressItem(index);

        AssertBool(!string.IsNullOrEmpty(ui.MessageText))
            .OverrideFailureMessage($"press of item {index} never reached the terminal")
            .IsTrue();
        return new PressResult(terminal.LastOrder, _ordering.DeliveredCount - before);
    }

    /// <summary>Checks that a press delivered exactly one live item, and returns that node.</summary>
    private static Node3D CheckDelivery(int index, PressResult press)
    {
        AssertThat(press.Delivered)
            .OverrideFailureMessage($"press of item {index} delivered {press.Delivered} items ({press.Order?.Status})")
            .IsEqual(1);
        AssertBool(press.Order is { Status: OrderStatus.Success, Spawned: not null })
            .OverrideFailureMessage($"press of item {index} was not a delivery: {press.Order?.Status}")
            .IsTrue();

        var node = press.Order!.Value.Spawned!;
        AssertBool(GodotObject.IsInstanceValid(node) && !node.IsQueuedForDeletion())
            .OverrideFailureMessage($"press of item {index} delivered a node that is not live")
            .IsTrue();
        return node;
    }

    /// <summary>Presses an item the terminal must deliver, and returns the node that press delivered.</summary>
    private Node3D PressForDelivery(OrderTerminal terminal, int index) => CheckDelivery(index, Press(terminal, index));

    /// <summary>Presses an item the terminal must refuse with <paramref name="status"/>, and nothing is delivered.</summary>
    private void PressForRefusal(OrderTerminal terminal, int index, OrderStatus status)
    {
        var press = Press(terminal, index);

        AssertThat(press.Delivered)
            .OverrideFailureMessage($"refused press of item {index} delivered {press.Delivered} items")
            .IsEqual(0);
        AssertBool(press.Order?.Status == status)
            .OverrideFailureMessage($"press of item {index} gave {press.Order?.Status}, expected {status}")
            .IsTrue();
    }

    /// <summary>
    /// Orders an item until the terminal refuses one for lack of room, and every order before that must be a delivery.
    /// A tall item claims more than one slot, so fewer than <see cref="Capacity" /> of them fit.
    /// </summary>
    private void OrderUntilFull(OrderTerminal terminal, int index)
    {
        // The bound only stops a grid that never fills from looping forever.
        for (var order = 0; order < 2 * Capacity; order++)
        {
            var press = Press(terminal, index);
            if (press.Delivered == 0)
            {
                AssertBool(press.Order?.Status == OrderStatus.DeliveryFull)
                    .OverrideFailureMessage($"the first refusal was {press.Order?.Status}, expected DeliveryFull")
                    .IsTrue();
                return;
            }

            CheckDelivery(index, press);
        }

        AssertBool(false).OverrideFailureMessage("the grid never refused an order").IsTrue();
    }

    /// <summary>What one press did: the terminal's latest result, and how many items ordering delivered.</summary>
    private readonly record struct PressResult(OrderResult? Order, int Delivered);
}
