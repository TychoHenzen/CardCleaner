using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The cashier's claim on the front sale: while a claim stands, the player cannot scan, pay or open the payment
///     screen on that sale, and the claimant's own scans and payments still go through. The basket is two items
///     priced 3 and 4, so the total is 7.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CheckoutClaimTest
{
    private const int StartingBalance = CheckoutCounterRig.StartingBalance;
    private const int BasketTotal = 7;

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
    public void TryClaim_refused_without_a_session()
    {
        var cashier = new object();

        AssertThat(_counter.TryClaim(cashier)).IsFalse();
        AssertThat(_counter.IsClaimed).IsFalse();
    }

    [TestCase]
    public void TryClaim_refused_after_the_player_scanned()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        AssertThat(_counter.Scan(0)).IsTrue();

        AssertThat(_counter.TryClaim(new object())).IsFalse();
        AssertThat(_counter.IsClaimed).IsFalse();
    }

    [TestCase]
    public void Second_claim_is_refused()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        var first = new object();
        var second = new object();

        AssertThat(_counter.TryClaim(first)).IsTrue();
        AssertThat(_counter.TryClaim(second)).IsFalse();
    }

    [TestCase]
    public void Player_scan_is_refused_while_claimed_and_says_so()
    {
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        AssertThat(_counter.TryClaim(new object())).IsTrue();

        AssertThat(_counter.Scan(0)).IsFalse();
        AssertThat(_counter.Session!.ScannedCount).IsEqual(0);
        AssertThat(_counter.StatusLabel!.Text).Contains("cashier");
    }

    [TestCase]
    public void Claimed_scans_do_not_raise_PaymentReady()
    {
        var raised = 0;
        _counter.PaymentReady += _ => raised++;
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        var cashier = new object();
        AssertThat(_counter.TryClaim(cashier)).IsTrue();

        AssertThat(_counter.Scan(0, cashier)).IsTrue();
        AssertThat(_counter.Scan(1, cashier)).IsTrue();

        AssertThat(_counter.Session!.State).IsEqual(CheckoutState.AwaitingPayment);
        AssertThat(raised).IsEqual(0);
    }

    [TestCase]
    public void Interact_does_not_raise_PaymentReady_while_claimed()
    {
        var raised = 0;
        _counter.PaymentReady += _ => raised++;
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4));
        var cashier = new object();
        AssertThat(_counter.TryClaim(cashier)).IsTrue();
        AssertThat(_counter.Scan(0, cashier)).IsTrue();
        AssertThat(_counter.Scan(1, cashier)).IsTrue();

        _counter.Interact();

        AssertThat(raised).IsEqual(0);
    }

    [TestCase]
    public void Claimed_card_payment_adds_the_total_to_money_and_awards_XP_once()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);
        var cashier = new object();
        AssertThat(_counter.TryClaim(cashier)).IsTrue();
        AssertThat(_counter.Scan(0, cashier)).IsTrue();
        AssertThat(_counter.Scan(1, cashier)).IsTrue();

        AssertThat(_counter.PayCard(BasketTotal, cashier)).IsTrue();

        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(1);
        AssertThat(_progression.Awards[0]).IsEqual(BasketTotal);
        AssertThat(customer.LeaveCount).IsEqual(1);
        AssertThat(_counter.Session).IsNull();
    }

    [TestCase]
    public void Claimed_cash_payment_with_ChangeParts_pays()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4);
        _counter.Checkout(customer);
        var cashier = new object();
        AssertThat(_counter.TryClaim(cashier)).IsTrue();
        AssertThat(_counter.Scan(0, cashier)).IsTrue();
        AssertThat(_counter.Scan(1, cashier)).IsTrue();
        var session = _counter.Session!;
        var change = PaymentRules.ChangeParts(PaymentRules.Change(session.Tendered, session.Total));

        AssertThat(_counter.PayCash(change, cashier)).IsTrue();

        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(1);
        AssertThat(customer.LeaveCount).IsEqual(1);
    }

    [TestCase]
    public void Claim_is_cleared_when_the_session_ends()
    {
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(first);
        _counter.Checkout(second);
        var cashier = new object();
        AssertThat(_counter.TryClaim(cashier)).IsTrue();
        AssertThat(_counter.Scan(0, cashier)).IsTrue();
        AssertThat(_counter.Scan(1, cashier)).IsTrue();
        AssertThat(_counter.PayCard(BasketTotal, cashier)).IsTrue();

        AssertThat(_counter.IsClaimed).IsFalse();
        AssertThat(_counter.TryClaim(new object())).IsTrue();
    }

    [TestCase]
    public void Unclaimed_player_flow_is_unchanged()
    {
        var raised = 0;
        _counter.PaymentReady += _ => raised++;
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);

        AssertThat(_counter.Scan(0)).IsTrue();
        AssertThat(raised).IsEqual(0);
        AssertThat(_counter.Scan(1)).IsTrue();
        AssertThat(raised).IsEqual(1);
        _counter.Interact();
        AssertThat(raised).IsEqual(2);
        AssertThat(_counter.PayCard(BasketTotal)).IsTrue();

        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(1);
        AssertThat(customer.LeaveCount).IsEqual(1);
    }
}
