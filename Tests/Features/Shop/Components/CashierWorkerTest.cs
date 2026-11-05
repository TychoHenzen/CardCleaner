using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Components;

/// <summary>
///     The hired cashier driven through its own <see cref="CashierWorker.Step" />. The basket is two items priced 3
///     and 4, so the total is 7. The worker's own frame loop is off (its process mode is disabled) except in the pause
///     case, so each test sets the clock by stepping it. A step is 0.1 seconds.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CashierWorkerTest
{
    private const int StartingBalance = CheckoutCounterRig.StartingBalance;
    private const int BasketTotal = 7;
    private const double StepSeconds = 0.1;

    // 300 steps is 30 seconds: enough for a whole sale (claim, two scans, payment) with room to spare.
    private const int StepLimit = 300;

    private MoneyService _money = null!;
    private CheckoutCounter _counter = null!;
    private OpenProgression _progression = null!;
    private FakeStaffService _staff = null!;
    private FakeShopDayService _day = null!;
    private CashierWorker _worker = null!;

    [BeforeTest]
    public async Task Setup()
    {
        var rig = await CheckoutCounterRig.Create();
        _money = rig.Money;
        _counter = rig.Counter;
        _progression = rig.Progression;

        _staff = new FakeStaffService(StaffDefaults.Cashier());
        _staff.HiredIds.Add(StaffDefaults.CashierId);
        _day = new FakeShopDayService { IsOpen = true };
        TestServices.Container.RegisterSingleton<IStaffService>(_staff);
        TestServices.Container.RegisterSingleton<IShopDayService>(_day);

        _worker = new CashierWorker { Counter = _counter, ProcessMode = Node.ProcessModeEnum.Disabled };
        TestServices.Inject(_worker);
        _counter.GetParent()!.AddChild(_worker);
    }

    [AfterTest]
    public void Teardown()
    {
        _counter.GetParent()?.Free();
        TestServices.Reset();
    }

    [TestCase]
    public void Cashier_completes_card_checkout_without_input()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);

        AssertThat(StepUntil(() => customer.LeaveCount == 1)).IsTrue();

        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(1);
        AssertThat(_progression.Awards[0]).IsEqual(BasketTotal);
        AssertThat(_counter.Session).IsNull();
        AssertThat(_counter.IsClaimed).IsFalse();
    }

    [TestCase]
    public void Cashier_completes_cash_checkout()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Cash, 3, 4);
        _counter.Checkout(customer);

        AssertThat(StepUntil(() => customer.LeaveCount == 1)).IsTrue();

        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(1);
        AssertThat(_counter.IsClaimed).IsFalse();
    }

    [TestCase]
    public void Balance_and_XP_increase_once()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);
        AssertThat(StepUntil(() => customer.LeaveCount == 1)).IsTrue();

        for (var i = 0; i < 50; i++)
            _worker.Step(StepSeconds);

        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(1);
    }

    [TestCase]
    public void Cashier_waits_while_shop_closed()
    {
        _day.IsOpen = false;
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);

        for (var i = 0; i < 100; i++)
            _worker.Step(StepSeconds);

        AssertThat(_counter.IsClaimed).IsFalse();
        AssertThat(customer.LeaveCount).IsEqual(0);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    public async Task Cashier_does_nothing_when_paused()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);
        var tree = _worker.GetTree();
        _worker.ProcessMode = Node.ProcessModeEnum.Inherit;
        tree.Paused = true;
        try
        {
            for (var i = 0; i < 5; i++)
                await ISceneRunner.SyncPhysicsFrame;

            AssertThat(_counter.IsClaimed).IsFalse();
            AssertThat(customer.LeaveCount).IsEqual(0);

            // Unpaused, the worker's own frame loop takes over the sale: one frame claims it.
            tree.Paused = false;
            await ISceneRunner.SyncPhysicsFrame;
            AssertThat(_counter.IsClaimed).IsTrue();
        }
        finally
        {
            tree.Paused = false;
        }
    }

    [TestCase]
    public void Cashier_skips_customer_when_player_started_session()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);
        AssertThat(_counter.Scan(0)).IsTrue();

        for (var i = 0; i < 100; i++)
            _worker.Step(StepSeconds);

        AssertThat(_counter.IsClaimed).IsFalse();
        AssertThat(customer.LeaveCount).IsEqual(0);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    public void Cashier_not_hired_does_nothing()
    {
        _staff.HiredIds.Clear();
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);

        for (var i = 0; i < 100; i++)
            _worker.Step(StepSeconds);

        AssertThat(_counter.IsClaimed).IsFalse();
        AssertThat(customer.LeaveCount).IsEqual(0);
    }

    [TestCase]
    public void Cashier_finishes_claimed_customer_after_dismiss()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);
        AssertThat(StepUntil(() => _counter.IsClaimed)).IsTrue();

        _staff.Dismiss(StaffDefaults.CashierId);

        AssertThat(StepUntil(() => customer.LeaveCount == 1)).IsTrue();
        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(1);
    }

    [TestCase]
    public void Customer_that_leaves_mid_flow_resets_without_paying()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);
        AssertThat(StepUntil(() => _counter.IsClaimed)).IsTrue();

        customer.Leave();
        for (var i = 0; i < 100; i++)
            _worker.Step(StepSeconds);

        AssertThat(_counter.IsClaimed).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertThat(_progression.Awards.Count).IsEqual(0);
    }

    [TestCase]
    public void Second_customer_not_claimed_until_first_paid()
    {
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(first);
        _counter.Checkout(second);
        AssertThat(StepUntil(() => _counter.IsClaimed)).IsTrue();
        AssertThat(ReferenceEquals(_counter.Front, first)).IsTrue();

        for (var i = 0; i < 30; i++)
            _worker.Step(StepSeconds);
        AssertThat(second.LeaveCount).IsEqual(0);

        AssertThat(StepUntil(() => first.LeaveCount == 1)).IsTrue();
        AssertThat(second.LeaveCount).IsEqual(0);
    }

    [TestCase]
    public void Pay_called_once_per_session()
    {
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(first);
        _counter.Checkout(second);

        AssertThat(StepUntil(() => first.LeaveCount == 1 && second.LeaveCount == 1)).IsTrue();
        for (var i = 0; i < 50; i++)
            _worker.Step(StepSeconds);

        AssertThat(_money.Balance).IsEqual(StartingBalance + BasketTotal + BasketTotal);
        AssertThat(_progression.Awards.Count).IsEqual(2);
    }

    [TestCase]
    public void Day_close_mid_claim_releases_cleanly()
    {
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 3, 4);
        _counter.Checkout(customer);
        AssertThat(StepUntil(() => _counter.Session?.ScannedCount == 1)).IsTrue();

        _day.IsOpen = false;
        _worker.Step(StepSeconds);
        AssertThat(_counter.IsClaimed).IsFalse();

        // ASSUMPTION (Low): a reopened shop does not resume a half-scanned sale; the player finishes it.
        _day.OpenShop();
        for (var i = 0; i < 100; i++)
            _worker.Step(StepSeconds);

        AssertThat(_counter.IsClaimed).IsFalse();
        AssertThat(customer.LeaveCount).IsEqual(0);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    public void Figure_visible_only_while_hired()
    {
        var figure = new Node3D();
        _worker.Figure = figure;

        _staff.Dismiss(StaffDefaults.CashierId);
        AssertThat(figure.Visible).IsFalse();

        AssertThat(_staff.Hire(StaffDefaults.CashierId)).IsEqual(StaffResult.Hired);
        AssertThat(figure.Visible).IsTrue();

        figure.Free();
    }

    /// <summary>Steps the worker until the condition holds, or returns false after <see cref="StepLimit" /> steps.
    /// </summary>
    private bool StepUntil(Func<bool> done)
    {
        for (var i = 0; i < StepLimit && !done(); i++)
            _worker.Step(StepSeconds);
        return done();
    }
}
