using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The shop XP award at checkout: one award per paid basket of the total paid, and none for any payment that
///     takes nothing. Each arrangement is the one the counter suite uses for the same rejection.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CheckoutCounterAwardTest
{
    private const int StartingBalance = CheckoutCounterRig.StartingBalance;

    private MoneyService _money = null!;
    private CheckoutCounter _counter = null!;
    private OpenProgression _progression = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var rig = await CheckoutCounterRig.Create();
        _money = rig.Money;
        _counter = rig.Counter;
        _progression = rig.Progression;
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CardPaymentAwardsTheTotalOnce()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 5));
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCard(7)).IsTrue();
        AssertThat(_counter.PayCard(7)).IsFalse();

        AssertThat(_progression.Awards.Count).IsEqual(1);
        AssertThat(_progression.Awards[0]).IsEqual(7);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CashPaymentAwardsTheTotal()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4));
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCash(new[] { 1, 1, 1 })).IsTrue();

        AssertThat(_progression.Awards.Count).IsEqual(1);
        AssertThat(_progression.Awards[0]).IsEqual(7);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WrongTotalAwardsNothing()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCard(8)).IsFalse();

        AssertThat(_progression.Awards.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void PaymentBeforeAllScannedAwardsNothing()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _counter.Scan(0);

        AssertThat(_counter.PayCard(7)).IsFalse();
        AssertThat(_counter.PayCash(new[] { 1 })).IsFalse();

        AssertThat(_progression.Awards.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DepartedCustomerAwardsNothing()
    {
        var gone = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(gone);
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 5));
        _counter.Scan(0);
        _counter.Scan(1);
        gone.Waiting = false;

        AssertThat(_counter.PayCard(7)).IsFalse();

        AssertThat(_progression.Awards.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RejectedBasketAwardsNothing()
    {
        // A zero-priced offer makes CheckoutSession throw, so the counter sends this customer away unpaid.
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 0, 4));
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 5));

        AssertThat(_progression.Awards.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MissingProgressionStillPays()
    {
        _counter.Progression = null;
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        _counter.Scan(0);
        _counter.Scan(1);

        AssertThat(_counter.PayCard(7)).IsTrue();

        AssertThat(_money.Balance).IsEqual(StartingBalance + 7);
        AssertThat(_progression.Awards.Count).IsEqual(0);
    }
}
