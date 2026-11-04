using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
public class CheckoutQueueTest
{
    private const int ThreeSpots = 3;

    [TestCase]
    [TestCategory("Unit")]
    public void EmptyQueueHasNoFront()
    {
        var queue = new CheckoutQueue();

        AssertThat(queue.Front).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AddingTheSameCustomerTwiceIsRefused()
    {
        var queue = new CheckoutQueue();
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3);

        AssertThat(queue.Add(customer)).IsTrue();
        AssertThat(queue.Add(customer)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExtraCustomersShareTheLastSpot()
    {
        var queue = new CheckoutQueue();
        var customers = Enumerable.Range(0, 4).Select(_ => new FakeCheckoutCustomer(PaymentMethod.Card, 3)).ToList();
        customers.ForEach(customer => queue.Add(customer));

        queue.AssignSpots(ThreeSpots, SpotAt);

        AssertThat(customers[0].Spots[0]).IsEqual(SpotAt(0));
        AssertThat(customers[1].Spots[0]).IsEqual(SpotAt(1));
        AssertThat(customers[2].Spots[0]).IsEqual(SpotAt(2));
        AssertThat(customers[3].Spots[0]).IsEqual(SpotAt(2));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RepeatingTheSameAssignmentTellsNobodyAgain()
    {
        var queue = new CheckoutQueue();
        var customers = Enumerable.Range(0, 4).Select(_ => new FakeCheckoutCustomer(PaymentMethod.Card, 3)).ToList();
        customers.ForEach(customer => queue.Add(customer));
        queue.AssignSpots(ThreeSpots, SpotAt);

        queue.AssignSpots(ThreeSpots, SpotAt);

        AssertThat(customers.All(customer => customer.Spots.Count == 1)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemovingTheFrontMovesTheLineForwardAndTellsTheNewFrontItsSpot()
    {
        var queue = new CheckoutQueue();
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 3);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 4);
        var third = new FakeCheckoutCustomer(PaymentMethod.Card, 5);
        queue.Add(first);
        queue.Add(second);
        queue.Add(third);
        queue.AssignSpots(ThreeSpots, SpotAt);

        queue.Remove(first);
        queue.AssignSpots(ThreeSpots, SpotAt);

        AssertThat(ReferenceEquals(queue.Front, second)).IsTrue();
        AssertThat(second.Spots.Count).IsEqual(2);
        AssertThat(second.Spots[^1]).IsEqual(SpotAt(0));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RemoveDepartedDropsOnlyCustomersWhoStoppedWaiting()
    {
        var queue = new CheckoutQueue();
        var gone = new FakeCheckoutCustomer(PaymentMethod.Card, 3) { Waiting = false };
        var waiting = new FakeCheckoutCustomer(PaymentMethod.Card, 4);
        queue.Add(gone);
        queue.Add(waiting);

        queue.RemoveDeparted();

        AssertThat(ReferenceEquals(queue.Front, waiting)).IsTrue();
        queue.Remove(waiting);
        AssertThat(queue.Front).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void NoSpotsMeansNobodyIsToldAndNoSpotIsRead()
    {
        var queue = new CheckoutQueue();
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3);
        queue.Add(customer);
        var reads = 0;

        queue.AssignSpots(0, spot =>
        {
            reads++;
            return SpotAt(spot);
        });

        AssertThat(customer.Spots.Count).IsEqual(0);
        AssertThat(reads).IsEqual(0);
    }

    private static QueueSpot SpotAt(int spot) => new(0f, 0f, spot);
}
