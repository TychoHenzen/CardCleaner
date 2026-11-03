using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

[TestSuite]
[RequireGodotRuntime]
public class CheckoutCounterTest
{
    private const int StartingBalance = CheckoutCounterRig.StartingBalance;
    private const float XzTolerance = 0.01f;
    private const int MaxProcessFrames = 10;

    private MoneyService _money = null!;
    private CheckoutCounter _counter = null!;
    private Marker3D[] _slots = null!;
    private Marker3D[] _itemSpots = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var rig = await CheckoutCounterRig.Create();
        _money = rig.Money;
        _counter = rig.Counter;
        _slots = rig.Slots;
        _itemSpots = rig.ItemSpots;
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CustomersGetQueueSlotsInArrivalOrder()
    {
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 3);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 4);
        var third = new FakeCheckoutCustomer(PaymentMethod.Card, 5);
        var fourth = new FakeCheckoutCustomer(PaymentMethod.Card, 6);

        _counter.Checkout(first);
        _counter.Checkout(second);
        _counter.Checkout(third);
        _counter.Checkout(fourth);

        AssertThat(first.Spots.Count).IsEqual(1);
        AssertThat(first.Spots[0]).IsEqual(SlotSpot(0));
        AssertThat(second.Spots[0]).IsEqual(SlotSpot(1));
        AssertThat(third.Spots[0]).IsEqual(SlotSpot(2));
        AssertThat(fourth.Spots.Count).IsEqual(1);
        AssertThat(fourth.Spots[0]).IsEqual(SlotSpot(2));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CustomersBeyondTheLastSlotShareItAndAdvanceWhenAPlaceFrees()
    {
        var customers = Enumerable.Range(0, 5).Select(_ => new FakeCheckoutCustomer(PaymentMethod.Card, 3)).ToList();
        customers.ForEach(_counter.Checkout);

        AssertThat(customers.All(customer => customer.Spots.Count == 1)).IsTrue();
        AssertThat(customers[4].Spots[0]).IsEqual(SlotSpot(2));

        _counter.Scan(0);
        AssertThat(_counter.PayCard(3)).IsTrue();

        AssertThat(customers[3].Spots[^1]).IsEqual(SlotSpot(2));
        AssertThat(customers[2].Spots[^1]).IsEqual(SlotSpot(1));
        AssertThat(customers[1].Spots[^1]).IsEqual(SlotSpot(0));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PayingForACustomerWhoAlreadyLeftTakesNothingAndDropsThem()
    {
        var gone = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var next = new FakeCheckoutCustomer(PaymentMethod.Card, 5);
        _counter.Checkout(gone);
        _counter.Checkout(next);
        _counter.Scan(0);
        _counter.Scan(1);

        gone.Waiting = false;

        AssertThat(_counter.PayCard(7)).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(gone.LeaveCount).IsEqual(0);
        AssertThat(ReferenceEquals(_counter.Front, next)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FrontBasketIsLaidOnTheCounter()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);

        _counter.Checkout(customer);

        AssertThat(ReferenceEquals(_counter.Front, customer)).IsTrue();
        var props = PropsOf(_counter);
        AssertThat(props.Count).IsEqual(2);
        AssertThat(XzDistance(PropFor(props, 0).GlobalPosition, _itemSpots[0].GlobalPosition) < XzTolerance).IsTrue();
        AssertThat(XzDistance(PropFor(props, 1).GlobalPosition, _itemSpots[1].GlobalPosition) < XzTolerance).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ScanMarksAnItemOnceAndRejectsRepeatAndStranger()
    {
        var raised = 0;
        _counter.PaymentReady += _ => raised++;
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));

        AssertThat(_counter.Scan(0)).IsTrue();
        AssertThat(_counter.Scan(0)).IsFalse();
        AssertThat(_counter.Scan(9)).IsFalse();
        AssertThat(raised).IsEqual(0);
        AssertThat(_counter.Scan(1)).IsTrue();
        AssertThat(raised).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PaymentBeforeAllScannedIsRejected()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _counter.Scan(0);

        AssertThat(_counter.PayCard(7)).IsFalse();
        AssertThat(_counter.PayCash(new[] { 1 })).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WrongCardTotalIsRejected()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCard(8)).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_counter.Session!.State).IsEqual(CheckoutState.AwaitingPayment);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WrongChangeIsRejected()
    {
        // Total 7, tendered 10, so the change due is 3.
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4));
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCash(new[] { 1, 1 })).IsFalse();
        AssertThat(_counter.PayCash(new[] { 5 })).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CorrectCardPaymentAddsTheTotalOnceFreesPropsAndAdvances()
    {
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 5);
        _counter.Checkout(first);
        _counter.Checkout(second);
        var props = PropsOf(_counter);
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCard(7)).IsTrue();
        AssertThat(_money.Balance).IsEqual(StartingBalance + 7);
        AssertThat(_counter.PayCard(7)).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance + 7);
        AssertThat(first.LeaveCount).IsEqual(1);
        AssertThat(props.All(prop => prop.IsQueuedForDeletion())).IsTrue();
        AssertThat(ReferenceEquals(_counter.Front, second)).IsTrue();
        AssertThat(second.Spots[^1]).IsEqual(SlotSpot(0));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CorrectCashPaymentWithExactChangeIsAccepted()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4));
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCash(new[] { 1, 1, 1 })).IsTrue();
        AssertThat(_money.Balance).IsEqual(StartingBalance + 7);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task WaitingFalseCustomerIsDroppedWithoutPayment()
    {
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 5);
        var ended = 0;
        _counter.SessionEnded += () => ended++;
        _counter.Checkout(first);
        _counter.Checkout(second);

        first.Waiting = false;
        // The counter drops the customer in its _Process, which may run a frame after SyncProcessFrame resolves.
        for (var frame = 0; frame < MaxProcessFrames && !ReferenceEquals(_counter.Front, second); frame++)
            await ISceneRunner.SyncProcessFrame;

        AssertThat(ReferenceEquals(_counter.Front, second)).IsTrue();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(ended).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BasketTheSessionRejectsIsSentAwayUnpaid()
    {
        // A zero-priced offer makes CheckoutSession throw, so the counter must send this customer away.
        var rejected = new FakeCheckoutCustomer(PaymentMethod.Card, 0, 4);
        var next = new FakeCheckoutCustomer(PaymentMethod.Card, 5);

        _counter.Checkout(rejected);
        _counter.Checkout(next);

        AssertThat(rejected.LeaveCount).IsEqual(1);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(ReferenceEquals(_counter.Front, next)).IsTrue();
        AssertThat(_counter.Session).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void InteractRaisesPaymentReadyOnlyWhileAwaitingPayment()
    {
        var raised = 0;
        _counter.PaymentReady += _ => raised++;
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));

        AssertThat(_counter.CanInteract).IsFalse();
        _counter.Interact();
        AssertThat(raised).IsEqual(0);

        _counter.Scan(0);
        _counter.Scan(1);
        AssertThat(raised).IsEqual(1);
        AssertThat(_counter.CanInteract).IsTrue();
        _counter.Interact();
        AssertThat(raised).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SameCustomerTwiceIsIgnored()
    {
        var repeated = new FakeCheckoutCustomer(PaymentMethod.Card, 3);
        var later = new FakeCheckoutCustomer(PaymentMethod.Card, 5);

        _counter.Checkout(repeated);
        _counter.Checkout(repeated);
        _counter.Checkout(later);

        // Queued once, so the later customer is second in line and takes slot 1, not slot 2.
        AssertThat(later.Spots.Count).IsEqual(1);
        AssertThat(later.Spots[0]).IsEqual(SlotSpot(1));
    }

    private QueueSpot SlotSpot(int index)
    {
        var position = _slots[index].GlobalPosition;
        return new QueueSpot(position.X, position.Y, position.Z);
    }

    private static List<CheckoutItemProp> PropsOf(Node parent) =>
        parent.GetChildren().OfType<CheckoutItemProp>().ToList();

    private static CheckoutItemProp PropFor(List<CheckoutItemProp> props, int itemId) =>
        props.Single(prop => prop.ItemId == itemId);

    private static float XzDistance(Vector3 a, Vector3 b) => new Vector2(a.X, a.Z).DistanceTo(new Vector2(b.X, b.Z));
}
