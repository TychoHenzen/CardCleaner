using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The staff wiring in the shop scene: the cashier worker and the Staff app are placed and connected, the Staff app
///     opens from its desktop icon, a hired cashier works a customer's checkout on its own, and the wage reaches the
///     daily bill book.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopStaffSceneTest
{
    private const string DesktopViewportPath = "PcDesktopViewport";
    private const string StaffIconPath = "PcDesktopViewport/PcDesktop/Icons/StaffIcon";
    private const string CounterPath = "World/Storefront/CheckoutCounter";
    private const string HireText = "Hire";
    private const string WageLineName = "Cashier wage";
    private const int LevelTwoPayment = 500;
    private const int BasketTotal = 30;
    private const int CheckoutSeconds = 60;
    private const float StepSeconds = 0.5f;
    private static readonly Vector2I RootWindowSize = new(1280, 720);

    private Node3D _shop = null!;
    private Window _root = null!;
    private Vector2I _originalRootSize;
    private PcScreen _screen = null!;
    private Control _app = null!;
    private CashierWorker _worker = null!;
    private CheckoutCounter _counter = null!;
    private StaffService _staff = null!;
    private ShopDayService _day = null!;
    private MoneyService _money = null!;
    private ShopProgression _progression = null!;
    private DailyBillBook _bills = null!;

    [BeforeTest]
    public async Task Setup()
    {
        _root = ((SceneTree)Engine.GetMainLoop()).Root;
        _originalRootSize = _root.Size;
        // A headless window is 64 x 64, which would collapse the desktop layout; it is sized before the scene enters.
        _root.Size = RootWindowSize;

        _shop = ShopOrderingRig.LoadShopWithServices();
        _worker = _shop.GetNode<CashierWorker>($"{CounterPath}/CashierWorker");
        _counter = _shop.GetNode<CheckoutCounter>(CounterPath);
        _staff = _shop.GetNode<StaffService>("Services/Staff");
        _day = _shop.GetNode<ShopDayService>("Services/ShopDayService");
        _money = _shop.GetNode<MoneyService>("Services/MoneyService");
        _progression = _shop.GetNode<ShopProgression>("Services/ShopProgression");
        _bills = new DailyBillBook(new IDailyBillSource[] { new RentBillSource(), new StaffWageBillSource(_staff) });

        // The shop is not the current scene, so no autoload injects its nodes; each one the suite drives is injected here.
        TestServices.Container.RegisterSingleton<IShopDayService>(_day);
        TestServices.Container.RegisterSingleton<IDailyBillBook>(_bills);
        // Opening the shop wakes the real spawner; it gets a random source and no frame loop, so it adds no customer
        // next to the fake one this suite checks out.
        var spawner = _shop.GetNode<CustomerSpawner>("Services/CustomerSpawner");
        spawner.Rng = RandomSources.Create(400UL);
        spawner.ProcessMode = Node.ProcessModeEnum.Disabled;
        _day.SaveCommand = new FakeSaveGame();
        TestServices.Inject(_day);
        TestServices.Inject(_worker);
        TestServices.Inject(_shop.GetNode<StaffApp>($"{DesktopViewportPath}/StaffApp"));

        AddNode(_shop);
        await ISceneRunner.SyncPhysicsFrame;
        await ISceneRunner.SyncPhysicsFrame;
        _screen = PcScreen.From(_shop);
        _app = _shop.GetNode<Control>($"{DesktopViewportPath}/StaffApp");
    }

    [AfterTest]
    public void Teardown()
    {
        _root.Size = _originalRootSize;
        _shop.QueueFree();
        ShopPauseRig.Teardown();
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void StaffAppAndWorkerAreWiredToTheShop()
    {
        var desktop = _shop.GetNode<PcDesktop>($"{DesktopViewportPath}/PcDesktop");

        AssertThat(_worker.Counter).IsEqual(_counter);
        AssertThat(_worker.Figure).IsEqual(_shop.GetNode<Node3D>($"{CounterPath}/CashierFigure"));
        AssertThat(desktop.StaffApp).IsEqual(_app);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task StaffAppIsReachableFromTheDesktopIcon()
    {
        _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal").Interact();
        await _screen.Click(_shop.GetNode<Button>(StaffIconPath));

        AssertBool(_app.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void FigureHiddenUntilHiredThenVisible()
    {
        var figure = _worker.Figure!;
        AssertBool(figure.Visible).IsFalse();

        MeetHireRequirements();
        _staff.Hire(StaffDefaults.CashierId);

        AssertBool(_staff.IsHired(StaffDefaults.CashierId)).IsTrue();
        AssertBool(figure.Visible).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task HireThroughTheAppThenACustomerChecksOutWithNoInput()
    {
        MeetHireRequirements();
        _day.OpenShop();

        // Hire through the Staff app: open the desktop, open the app, press Hire on the cashier's card.
        _shop.GetNode<ScreenTerminal>("World/Markers/PcLocation/PcTerminal").Interact();
        await _screen.Click(_shop.GetNode<Button>(StaffIconPath));
        var hire = FindButtonWithText(CardFor($"Fee: {StaffDefaults.CashierHireFee}"), HireText);
        AssertBool(hire != null && !hire.Disabled).IsTrue();
        await _screen.Click(hire!);
        await ISceneRunner.SyncProcessFrame;

        AssertBool(_staff.IsHired(StaffDefaults.CashierId)).IsTrue();
        AssertInt(_money.Balance).IsEqual(0);

        // The cashier, not the test, drives the checkout: the counter is only given the customer and the worker is stepped.
        int xpBefore = _progression.Xp;
        bool paymentRaised = false;
        _counter.PaymentReady += _ => paymentRaised = true;
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 10, 20);
        _counter.Checkout(customer);

        for (var elapsed = 0f; elapsed < CheckoutSeconds && customer.LeaveCount == 0; elapsed += StepSeconds)
        {
            _worker.Step(StepSeconds);
        }

        AssertInt(customer.LeaveCount).IsEqual(1);
        AssertInt(_money.Balance).IsEqual(BasketTotal);
        AssertInt(_progression.Xp).IsEqual(xpBefore + CheckoutXp.For(BasketTotal));
        AssertBool(paymentRaised).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void WageLineShowsInTheDailyBillBook()
    {
        MeetHireRequirements();
        _staff.Hire(StaffDefaults.CashierId);

        AssertBool(_bills.LinesFor(1).Any(line => line.Name == WageLineName)).IsTrue();
    }

    // Reaches shop level 2 and holds exactly the hire fee, so a hire leaves the balance at zero.
    private void MeetHireRequirements()
    {
        _money.TrySpend(_money.Balance);
        _money.Add(StaffDefaults.CashierHireFee);
        _progression.AwardCheckout(LevelTwoPayment);
    }

    private PanelContainer CardFor(string labelText) =>
        _app.FindChildren("*", "PanelContainer", true, false).OfType<PanelContainer>()
            .First(panel => panel.FindChildren("*", "Label", true, false).OfType<Label>()
                .Any(label => label.Text == labelText));

    private static Button? FindButtonWithText(Node root, string text)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Button button && button.Text == text)
                return button;
            var found = FindButtonWithText(child, text);
            if (found != null)
                return found;
        }
        return null;
    }

    // Stands in for the slot writer so the day close writes no file during the scene test.
    private sealed class FakeSaveGame : ISaveGameCommand
    {
        public bool ManualSave() => true;
    }
}
