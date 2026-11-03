using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;
using static CardCleaner.Tests.Scenes.ShopCustomerRig;

namespace CardCleaner.Tests.Scenes;

/// <summary>
/// Customers in the shop scene at the checkout: a customer that reaches the counter hands its basket over and waits
/// in line until it is told to leave, and a customer that gives up leaves at once without a sale.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopCustomerCheckoutSceneTest
{
    private const float QueueTolerance = 0.7f;
    private const float QueueOffsetZ = 1.0f;
    private const int BoosterPrice = 4;

    private ShopCustomerRig _rig = null!;
    private RecordingHandoff _handoff = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _rig = await ShopCustomerRig.Create();
        _handoff = new RecordingHandoff();
        _rig.Spawner.Handoff = _handoff;
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task CustomerHandsTheBasketOverAndWaitsAtTheCounter()
    {
        var slot = _rig.StockBooster();
        var balanceBefore = _rig.Money.Balance;

        var customer = _rig.Spawner.SpawnNow();
        AssertThat(customer).IsNotNull();
        await WaitUntil(() => _handoff.Customer != null);

        var handed = _handoff.Customer!;
        AssertBool(Gone(customer!)).IsFalse();
        AssertBool(handed.Waiting).IsTrue();
        AssertThat(handed.Basket.Total).IsEqual(BoosterPrice);
        AssertBool(handed.Method == PaymentMethod.Card || handed.Method == PaymentMethod.Cash).IsTrue();
        AssertBool(slot.HasCards).IsFalse();
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task QueuedCustomerWalksToItsSpotAndLeavesWhenTold()
    {
        _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow();
        await WaitUntil(() => _handoff.Customer != null);
        var queued = _handoff.Customer!;

        var counter = _rig.Marker("CustomerCounter");
        var target = new QueueSpot(counter.X, counter.Y, counter.Z + QueueOffsetZ);
        queued.QueueAt(target);
        var spot = new Vector3(target.X, target.Y, target.Z);
        await WaitUntil(() => XzDistance(customer!.GlobalPosition, spot) <= QueueTolerance);

        AssertBool(Gone(customer!)).IsFalse();
        AssertBool(queued.Waiting).IsTrue();

        queued.Leave();
        await WaitUntil(() => Gone(customer!));
        AssertBool(queued.Waiting).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task GivingUpLeavesAtOnceWithoutASale()
    {
        _rig.StockBooster();
        var customer = _rig.Spawner.SpawnNow();
        await WaitUntil(() => _handoff.Customer != null);
        var given = _handoff.Customer!;
        var balanceBefore = _rig.Money.Balance;

        // ASSUMPTION (Low): the handed-over customer is the rig's real Customer node, which owns GiveUp; the
        // ICheckoutCustomer contract does not declare it.
        ((Customer)given).GiveUp();
        AssertBool(given.Waiting).IsFalse();
        await WaitUntil(() => Gone(customer!));
        AssertThat(_rig.Money.Balance).IsEqual(balanceBefore);
    }

    /// <summary>
    /// Stores the customer it is handed and neither pays nor tells it to leave, so the test controls both.
    /// </summary>
    private sealed class RecordingHandoff : ICheckoutHandoff
    {
        public ICheckoutCustomer? Customer { get; private set; }

        public void Checkout(ICheckoutCustomer customer)
        {
            Customer = customer;
        }
    }
}
