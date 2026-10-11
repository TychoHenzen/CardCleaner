using System.Collections.Generic;
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
    private const float ClearanceBottom = 0.02f;
    private const float ClearanceTop = 2.2f;

    // A cabinet with its holders spans two columns, so the second goes to the first free cell, the far column.
    private static readonly int[] CabinetSlots = [0, 2];

    private Node3D _shop = null!;
    private Node3D _workshop = null!;
    private OrderTerminal _pc = null!;
    private OrderTerminal _terminal = null!;
    private OrderTerminalUi _pcUi = null!;
    private OrderTerminalUi _ui = null!;
    private MoneyService _money = null!;
    private TerminalPresses _presses = null!;
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
        _presses = new TerminalPresses(_shop.GetNode<OrderingService>("Services/OrderingService"), Capacity);

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
    public void DeliveryClearanceIsFreeOfWorkshopGeometry()
    {
        var clearance = _delivery.FloorClearance;
        AssertBool(clearance.HasArea())
            .OverrideFailureMessage($"the delivery point declares no floor clearance: {clearance}")
            .IsTrue();

        var space = _shop.GetWorld3D().DirectSpaceState;
        var height = ClearanceTop - ClearanceBottom;
        var centre = clearance.GetCenter();
        // The walls stand on the clearance's edges, so a box exactly that size only touches them. Inset it on X and Z.
        var size = new Vector3(clearance.Size.X - 2f * ProbeInset, height, clearance.Size.Y - 2f * ProbeInset);
        var query = new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = size },
            Transform = new Transform3D(Basis.Identity,
                _delivery.GlobalPosition + new Vector3(centre.X, ClearanceBottom + height / 2f, centre.Y)),
            CollideWithBodies = true,
            CollideWithAreas = false
        };

        var hits = space.IntersectShape(query, 64);
        var colliders = string.Join(", ",
            hits.Select(hit => hit["collider"].Obj is Node node ? node.GetPath().ToString() : "?"));
        AssertThat(hits.Count)
            .OverrideFailureMessage($"bodies inside the delivery clearance: {colliders}")
            .IsEqual(0);
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
        var first = _presses.PressForDelivery(_terminal, cabinet);
        _presses.OrderUntilFull(_terminal, cabinet);

        first.QueueFree();
        var replacement = _presses.PressForDelivery(_terminal, cabinet);

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
        _presses.OrderUntilFull(_terminal, cabinet);
        var balance = _money.Balance;
        var spawned = _shop.GetNode("World").GetChildCount();

        _presses.PressForRefusal(_terminal, cabinet, OrderStatus.DeliveryFull);

        AssertThat(_money.Balance).IsEqual(balance);
        AssertThat(_shop.GetNode("World").GetChildCount()).IsEqual(spawned);
        AssertBool(_ui.MessageText.StartsWith("The delivery area is full")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderedCabinetsFillTheGridTwoAtATimeAndNeverReachEachOthersHolders()
    {
        _money.Add(TopUp);
        _terminal.Interact();

        var cabinets = _presses.OrderUntilFull(_terminal, IndexOf("arcade_cabinet"));

        AssertThat(string.Join(", ", DeliveredFootprints.OverlapsBetween(cabinets))).IsEmpty();
        var offsets = string.Join(", ", cabinets.Select(cabinet => cabinet.GlobalPosition - _delivery.GlobalPosition));
        AssertThat(cabinets.Count)
            .OverrideFailureMessage($"delivered {cabinets.Count} cabinets at offsets {offsets}")
            .IsEqual(CabinetSlots.Length);
        for (var i = 0; i < cabinets.Count; i++)
        {
            var expected = _delivery.GlobalPosition + OrderingService.SlotOffset(CabinetSlots[i]);
            AssertBool(cabinets[i].GlobalPosition.IsEqualApprox(expected))
                .OverrideFailureMessage(
                    $"cabinet {i} is at {cabinets[i].GlobalPosition}, expected slot {CabinetSlots[i]}")
                .IsTrue();
        }

        foreach (var cabinet in cabinets)
            AssertThat(string.Join(", ", ProbeAgainstWorkshop(cabinet))).IsEmpty();
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
            var delivered = _presses.PressForDelivery(_terminal, wiring);

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
        _presses.PressForDelivery(_terminal, IndexOf("wiring"));
        _presses.PressForDelivery(_terminal, IndexOf("wiring"));
        _terminal.Close();

        _pc.Interact();
        var spawned = _presses.PressForDelivery(_pc, 0);
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
            var workshopItem = _presses.PressForDelivery(_terminal, wiringIndex);
            _terminal.Close();
            expected -= wiringPrice;
            AssertThat(_money.Balance).IsEqual(expected);
            AssertBool(workshopItem.GlobalPosition.IsEqualApprox(
                _delivery.GlobalPosition + OrderingService.SlotOffset(round))).IsTrue();

            _pc.Interact();
            var shopItem = _presses.PressForDelivery(_pc, boxIndex);
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
        var cabinet = (RigidBody3D)_presses.PressForDelivery(_terminal, IndexOf("arcade_cabinet"));
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
    /// Probes every collision shape of a delivered item in its place against the workshop's bodies. The item's own
    /// bodies are excluded, and each shape is lifted by the probe inset so the floor it stands on is not reported.
    /// Footprint triggers (holders under a node in the delivery_footprint group) are probed too, since the item carries
    /// them. Other triggers are skipped, so a report means a solid shape or footprint holder inside workshop geometry.
    /// </summary>
    private List<string> ProbeAgainstWorkshop(Node3D item)
    {
        var space = _shop.GetWorld3D().DirectSpaceState;
        var own = new Godot.Collections.Array<Rid>();
        if (item is CollisionObject3D root)
            own.Add(root.GetRid());
        foreach (var body in item.FindChildren("*", nameof(CollisionObject3D), true, false).OfType<CollisionObject3D>())
            own.Add(body.GetRid());

        var hits = new List<string>();
        foreach (var collider in item.FindChildren("*", nameof(CollisionShape3D), true, false)
                     .OfType<CollisionShape3D>()
                     .Where(c => c is { Disabled: false, Shape: not null }
                                  && (c.GetParent() is not Area3D || CountsAsFootprint(c, item))))
        {
            var query = new PhysicsShapeQueryParameters3D
            {
                Shape = collider.Shape,
                Transform = new Transform3D(collider.GlobalBasis, collider.GlobalPosition + Vector3.Up * ProbeInset),
                CollideWithBodies = true,
                CollideWithAreas = false,
                Exclude = own
            };
            if (space.IntersectShape(query, 1).Count > 0)
                hits.Add($"{item.GetPathTo(collider)} is inside workshop geometry");
        }

        return hits;
    }

    // A trigger counts toward the probe only under a node in the delivery footprint group, as DeliveryMarker counts it.
    private static bool CountsAsFootprint(Node collider, Node item)
    {
        for (var step = collider.GetParent(); step != null && step != item; step = step.GetParent())
        {
            if (step.IsInGroup(DeliveryMarker.FootprintGroup))
                return true;
        }

        return false;
    }
}
