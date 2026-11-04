using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Ordering flow of the workshop terminal inside the shop scene: freed slots are reused, a full area refuses with no
/// charge, ordering never locks up, the shop delivery grid stays untouched, and the balance stays consistent when
/// the two terminals order alternately.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopWorkshopOrderFlowSceneTest
{
    private const int Rounds = 3;

    private WorkshopDeliveryRig _rig = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = new WorkshopDeliveryRig();
        AddNode(_rig.Shop);
        await WorkshopDeliveryRig.SettleAsync();
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemovingADeliveredItemFreesItsSlotForTheNextOrder()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        _rig.Terminal.Interact();
        var cabinet = _rig.IndexOf("arcade_cabinet");
        var first = _rig.Presses.PressForDelivery(_rig.Terminal, cabinet);
        _rig.Presses.OrderUntilFull(_rig.Terminal, cabinet);

        first.QueueFree();
        var replacement = _rig.Presses.PressForDelivery(_rig.Terminal, cabinet);

        AssertBool(replacement.GlobalPosition.IsEqualApprox(
                _rig.Delivery.GlobalPosition + OrderingService.SlotOffset(0)))
            .IsTrue();
        AssertThat(_rig.Ui.MessageText.Contains("Ordered")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FullDeliveryAreaRejectsTheNextTallOrderWithAMessageAndNoCharge()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        _rig.Terminal.Interact();
        var cabinet = _rig.IndexOf("arcade_cabinet");
        _rig.Presses.OrderUntilFull(_rig.Terminal, cabinet);
        var balance = _rig.Money.Balance;
        var spawned = _rig.Shop.GetNode("World").GetChildCount();

        _rig.Presses.PressForRefusal(_rig.Terminal, cabinet, OrderStatus.DeliveryFull);

        AssertThat(_rig.Money.Balance).IsEqual(balance);
        AssertThat(_rig.Shop.GetNode("World").GetChildCount()).IsEqual(spawned);
        AssertBool(_rig.Ui.MessageText.StartsWith("The delivery area is full")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OrderingNeverLocksAfterManyDeliveriesOfSmallItems()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        _rig.Terminal.Interact();
        var wiring = _rig.IndexOf("wiring");
        var ceilingClearance = _rig.Delivery.GlobalPosition.Y + _rig.Delivery.MaxStackHeight;

        for (var order = 0; order < 4 * WorkshopDeliveryRig.Capacity; order++)
        {
            var delivered = _rig.Presses.PressForDelivery(_rig.Terminal, wiring);

            AssertBool(_rig.Ui.MessageText.Contains("Ordered")).IsTrue();
            AssertBool(delivered.GlobalPosition.Y < ceilingClearance).IsTrue();
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ConveyorsStackOnConveyorsOnceTheGroundIsFull()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        _rig.Terminal.Interact();
        var conveyor = _rig.IndexOf("conveyor");
        for (var order = 0; order < WorkshopDeliveryRig.Capacity; order++)
            _rig.Presses.PressForDelivery(_rig.Terminal, conveyor);

        // The belt's collider is tilted, so its bounds dip below the conveyor's feet. It must still stack.
        var stacked = _rig.Presses.PressForDelivery(_rig.Terminal, conveyor);

        var offset = stacked.GlobalPosition - _rig.Delivery.GlobalPosition;
        var slot = OrderingService.SlotOffset(0);
        AssertBool(Mathf.IsEqualApprox(offset.X, slot.X) && Mathf.IsEqualApprox(offset.Z, slot.Z) && offset.Y > 0f)
            .OverrideFailureMessage($"the stacked conveyor is at {offset} from the marker, expected above slot 0")
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WorkshopOrdersLeaveTheShopDeliveryGridUntouched()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        _rig.Terminal.Interact();
        _rig.Presses.PressForDelivery(_rig.Terminal, _rig.IndexOf("wiring"));
        _rig.Presses.PressForDelivery(_rig.Terminal, _rig.IndexOf("wiring"));
        _rig.Terminal.Close();

        _rig.Pc.Interact();
        var spawned = _rig.PcPresses.PressForDelivery(0);
        AssertBool(spawned.GlobalPosition.IsEqualApprox(
                _rig.ShopDelivery.GlobalPosition + OrderingService.SlotOffset(0)))
            .IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AlternatingOrdersFromBothTerminalsKeepOneConsistentBalanceAndTwoSeparateGrids()
    {
        _rig.Money.Add(WorkshopDeliveryRig.TopUp);
        var expected = _rig.Money.Balance;
        var wiringIndex = _rig.IndexOf("wiring");
        var wiringPrice = _rig.Terminal.Catalog!.Items[wiringIndex].Price;
        var boxIndex = CardboardBoxIndex();
        var boxPrice = _rig.PcStock.Catalog!.Items[boxIndex].Price;

        for (var round = 0; round < Rounds; round++)
        {
            _rig.Terminal.Interact();
            var workshopItem = _rig.Presses.PressForDelivery(_rig.Terminal, wiringIndex);
            _rig.Terminal.Close();
            expected -= wiringPrice;
            AssertThat(_rig.Money.Balance).IsEqual(expected);
            AssertBool(workshopItem.GlobalPosition.IsEqualApprox(
                _rig.Delivery.GlobalPosition + OrderingService.SlotOffset(round))).IsTrue();

            _rig.Pc.Interact();
            var shopItem = _rig.PcPresses.PressForDelivery(boxIndex);
            _rig.Pc.Close();
            expected -= boxPrice;
            AssertThat(_rig.Money.Balance).IsEqual(expected);
            AssertBool(shopItem.GlobalPosition.IsEqualApprox(
                _rig.ShopDelivery.GlobalPosition + OrderingService.SlotOffset(round))).IsTrue();

            AssertThat(_rig.PcStock.BalanceText).IsEqual($"Balance: {expected}");
            AssertThat(_rig.Ui.BalanceText).IsEqual($"Balance: {expected}");
        }
    }

    private int CardboardBoxIndex() =>
        _rig.PcStock.Catalog!.Items.ToList().FindIndex(i => i.Id == "cardboard_box");
}
