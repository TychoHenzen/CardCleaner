using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;
using Godot;
using StaffApp = CardCleaner.Scripts.Features.Shop.Ui.StaffApp;
using FakeCheckoutCustomer = CardCleaner.Tests.Features.Shop.FakeCheckoutCustomer;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The failure and edge paths of the hired cashier on the real shop scene: refused hires, a closed shop, a paused
///     tree, a player scan during a claim, a customer who leaves mid-claim, a dismissal mid-claim and two customers in
///     a row.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopStaffFailureSceneTest
{
    private const string DesktopViewportPath = "PcDesktopViewport";
    private const string CounterPath = "World/Storefront/CheckoutCounter";
    private const string ServingMessage = "The cashier is serving this customer";
    private const int LevelTwoPayment = 500;
    private const int CheckoutSeconds = 60;
    private const int ClaimSteps = 2;
    private const float StepSeconds = 0.5f;

    private Node3D _shop = null!;
    private CashierWorker _worker = null!;
    private CheckoutCounter _counter = null!;
    private StaffService _staff = null!;
    private ShopDayService _day = null!;
    private MoneyService _money = null!;
    private ShopProgression _progression = null!;
    private StaffApp _app = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _shop = ShopOrderingRig.LoadShopWithServices();
        _worker = _shop.GetNode<CashierWorker>($"{CounterPath}/CashierWorker");
        _counter = _shop.GetNode<CheckoutCounter>(CounterPath);
        _staff = _shop.GetNode<StaffService>("Services/Staff");
        _day = _shop.GetNode<ShopDayService>("Services/ShopDayService");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
        _app = _shop.GetNode<StaffApp>($"{DesktopViewportPath}/StaffApp");

        var bills = new DailyBillBook(new IDailyBillSource[] { new RentBillSource(), new StaffWageBillSource(_staff) });
        TestServices.Container.RegisterSingleton<IShopDayService>(_day);
        TestServices.Container.RegisterSingleton<IDailyBillBook>(bills);
        // Opening the shop wakes the real spawner; it gets a random source and no frame loop, so it adds no customer.
        var spawner = _shop.GetNode<CustomerSpawner>("Services/CustomerSpawner");
        spawner.Rng = RandomSources.Create(402UL);
        spawner.ProcessMode = Node.ProcessModeEnum.Disabled;
        _day.SaveCommand = new NoOpSaveGame();
        TestServices.Inject(_day);
        TestServices.Inject(_worker);
        TestServices.Inject(_app);

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
    }

    [AfterTest]
    public void Teardown()
    {
        _shop.QueueFree();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Hire_when_locked_leaves_money_and_roster_unchanged()
    {
        // Level 1: the shop has earned no XP.
        _money.TrySpend(_money.Balance);
        _money.Add(StaffDefaults.CashierHireFee);

        AssertThat(_staff.Hire(StaffDefaults.CashierId)).IsEqual(StaffResult.Locked);

        AssertInt(_money.Balance).IsEqual(StaffDefaults.CashierHireFee);
        AssertBool(_staff.IsHired(StaffDefaults.CashierId)).IsFalse();
        AssertBool(_worker.Figure!.Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Hire_when_too_poor_shows_the_reason_in_the_Staff_app_and_charges_nothing()
    {
        _progression.AwardCheckout(LevelTwoPayment);
        _money.TrySpend(_money.Balance);
        _money.Add(StaffDefaults.CashierHireFee - 1);
        _app.Visible = true;
        var hire = (Button)_app.FindChild("Hire", true, false)!;

        hire.EmitSignal(BaseButton.SignalName.Pressed);
        await ISceneRunner.SyncProcessFrame;
        await ISceneRunner.SyncProcessFrame;

        var message = (Label)_app.FindChild("Message", true, false)!;
        AssertThat(message.Text).IsEqual($"Not enough money: {StaffDefaults.CashierHireFee} needed.");
        AssertInt(_money.Balance).IsEqual(StaffDefaults.CashierHireFee - 1);
        AssertBool(_staff.IsHired(StaffDefaults.CashierId)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Cashier_does_not_serve_while_the_shop_is_closed()
    {
        HireCashier();
        int balance = _money.Balance;
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 10, 20);
        _counter.Checkout(customer);

        RunUntil(() => customer.LeaveCount > 0);

        AssertInt(customer.LeaveCount).IsEqual(0);
        AssertBool(_counter.IsClaimed).IsFalse();
        AssertInt(_money.Balance).IsEqual(balance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Cashier_waits_while_the_tree_is_paused()
    {
        HireCashier();
        _day.OpenShop();
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 10, 20);
        _counter.Checkout(customer);
        var tree = (SceneTree)Engine.GetMainLoop();
        tree.Paused = true;

        for (var i = 0; i < ClaimSteps; i++)
            await ISceneRunner.SyncProcessFrame;

        AssertBool(_counter.IsClaimed).IsFalse();

        // The same shop claims the customer on its next frame once the tree runs again.
        tree.Paused = false;
        for (var i = 0; i < ClaimSteps; i++)
            await ISceneRunner.SyncProcessFrame;

        AssertBool(_counter.IsClaimed).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Player_scan_during_a_cashier_claim_is_refused_with_a_message_and_the_payment_screen_stays_closed()
    {
        HireCashier();
        _day.OpenShop();
        bool paymentRaised = false;
        _counter.PaymentReady += _ => paymentRaised = true;
        _counter.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 10));
        _worker.Step(StepSeconds);
        AssertBool(_counter.IsClaimed).IsTrue();

        AssertBool(_counter.Scan(0)).IsFalse();
        _counter.Interact();

        AssertBool(_counter.StatusLabel!.Text.Contains(ServingMessage, StringComparison.Ordinal)).IsTrue();
        AssertInt(_counter.Session!.ScannedCount).IsEqual(0);
        AssertBool(paymentRaised).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Customer_freed_mid_claim_releases_the_claim_and_a_new_customer_is_served()
    {
        HireCashier();
        _day.OpenShop();
        int balance = _money.Balance;
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 10, 20);
        _counter.Checkout(first);
        _worker.Step(StepSeconds);
        AssertBool(_counter.IsClaimed).IsTrue();

        // The customer's node goes away: the line sees a customer who is no longer waiting.
        first.Leave();
        _worker.Step(StepSeconds);
        await ISceneRunner.SyncProcessFrame;
        AssertBool(_counter.IsClaimed).IsFalse();

        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 7);
        _counter.Checkout(second);
        await ISceneRunner.SyncProcessFrame;
        RunUntil(() => second.LeaveCount > 0);

        AssertInt(second.LeaveCount).IsEqual(1);
        AssertInt(first.LeaveCount).IsEqual(1);
        AssertInt(_money.Balance).IsEqual(balance + 7);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Dismissing_while_a_customer_is_claimed_lets_that_customer_finish()
    {
        HireCashier();
        _day.OpenShop();
        int balance = _money.Balance;
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 10, 20);
        _counter.Checkout(customer);
        _worker.Step(StepSeconds);
        AssertBool(_counter.IsClaimed).IsTrue();

        AssertThat(_staff.Dismiss(StaffDefaults.CashierId)).IsEqual(StaffResult.Dismissed);
        RunUntil(() => customer.LeaveCount > 0);

        AssertInt(customer.LeaveCount).IsEqual(1);
        AssertInt(_money.Balance).IsEqual(balance + 30);
        AssertBool(_worker.Figure!.Visible).IsFalse();

        // Nobody is hired now, so the next customer waits for the player.
        var next = new FakeCheckoutCustomer(PaymentMethod.Card, 5);
        _counter.Checkout(next);
        RunUntil(() => next.LeaveCount > 0);
        AssertInt(next.LeaveCount).IsEqual(0);
        AssertBool(_counter.IsClaimed).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Two_customers_are_each_paid_once_in_queue_order()
    {
        HireCashier();
        _day.OpenShop();
        int balance = _money.Balance;
        int xp = _progression.Xp;
        var first = new FakeCheckoutCustomer(PaymentMethod.Card, 10, 20);
        var second = new FakeCheckoutCustomer(PaymentMethod.Card, 12);
        _counter.Checkout(first);
        _counter.Checkout(second);

        RunUntil(() => first.LeaveCount > 0);
        AssertInt(second.LeaveCount).IsEqual(0);
        AssertInt(_money.Balance).IsEqual(balance + 30);

        RunUntil(() => second.LeaveCount > 0);

        AssertInt(first.LeaveCount).IsEqual(1);
        AssertInt(second.LeaveCount).IsEqual(1);
        AssertInt(_money.Balance).IsEqual(balance + 42);
        AssertInt(_progression.Xp).IsEqual(xp + CheckoutXp.For(30) + CheckoutXp.For(12));
    }

    // Reaches shop level 2 and holds exactly the hire fee plus some change, then hires.
    private void HireCashier()
    {
        _progression.AwardCheckout(LevelTwoPayment);
        _money.TrySpend(_money.Balance);
        _money.Add(StaffDefaults.CashierHireFee);
        AssertThat(_staff.Hire(StaffDefaults.CashierId)).IsEqual(StaffResult.Hired);
    }

    // Steps the cashier on a manual clock until the condition holds or the time budget runs out.
    private void RunUntil(Func<bool> done)
    {
        for (var elapsed = 0f; elapsed < CheckoutSeconds && !done(); elapsed += StepSeconds)
            _worker.Step(StepSeconds);
    }
}
