using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.SaveSlots.Services;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The staff roster through a real slot save and reload of the shop scene, the bad-data paths of its load, and the
///     wage line at the day close.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopStaffSaveSceneTest
{
    private const string FigurePath = "World/Storefront/CheckoutCounter/CashierFigure";
    private const string HiredKey = "hired";
    private const int SpareMoney = 137;
    private const int LevelTwoPayment = 500;
    private const int LevelThreePayment = 1500;
    private const int MaxFrames = 2400;
    private const int IdleFrames = 120;
    private const int SettleRounds = 3;
    private const int ShortBalance = 10;
    private const int ClaimSteps = 4;
    private const float StepSeconds = 0.5f;

    private InMemorySlotFileSystem _files = null!;
    private ShopExpansionRig _rig = null!;
    private ShopExpansionRig? _second;

    [BeforeTest]
    public async Task Setup()
    {
        _files = new InMemorySlotFileSystem();
        _rig = await ShopExpansionRig.StartAsync(_files);
    }

    [AfterTest]
    public void Teardown()
    {
        _second?.Close();
        _second = null;
        _rig.Close();
        ShopPauseRig.Teardown();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Hired_cashier_survives_save_and_reload()
    {
        HireCashier();
        AssertInt(_rig.Money.Balance).IsEqual(SpareMoney);
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        await SettleAsync();

        AssertBool(_second.Staff.IsHired(StaffDefaults.CashierId)).IsTrue();
        AssertBool(_second.Shop.GetNode<Node3D>(FigurePath).Visible).IsTrue();
        // The fee was paid before the save and is not charged again by the load.
        AssertInt(_second.Money.Balance).IsEqual(SpareMoney);
        var wages = new StaffWageBillSource(_second.Staff).GetBillLines(1).ToList();
        AssertInt(wages.Count).IsEqual(1);
        AssertInt(wages[0].Amount).IsEqual(StaffDefaults.CashierDailyWage);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Unhired_save_loads_with_nobody_hired()
    {
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        await SettleAsync();

        AssertBool(_second.Staff.IsHired(StaffDefaults.CashierId)).IsFalse();
        AssertBool(_second.Shop.GetNode<Node3D>(FigurePath).Visible).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Save_without_the_staff_key_loads_as_nobody_hired()
    {
        var log = new RecordingLog();
        using var sink = Capture(log);

        _rig.Staff.Load(new NodeSave());

        AssertBool(_rig.Staff.IsHired(StaffDefaults.CashierId)).IsFalse();
        AssertInt(log.Warnings.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Unknown_role_id_in_a_save_is_dropped_with_a_warning_and_valid_ones_kept()
    {
        var log = new RecordingLog();
        using var sink = Capture(log);
        var save = new NodeSave();
        save.SetOrAddProperty(HiredKey, "[\"cashier\",\"ghost\",\"cashier\"]");

        _rig.Staff.Load(save);

        AssertInt(log.Warnings.Count).IsEqual(1);
        AssertThat(_rig.Staff.HiredRoles.Select(role => role.Id).ToList())
            .IsEqual(new List<string> { StaffDefaults.CashierId });
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Unreadable_staff_json_is_warned_and_ignored()
    {
        HireCashier();
        var log = new RecordingLog();
        using var sink = Capture(log);
        var save = new NodeSave();
        save.SetOrAddProperty(HiredKey, "{not a list");

        _rig.Staff.Load(save);

        AssertInt(log.Warnings.Count).IsEqual(1);
        AssertBool(_rig.Staff.IsHired(StaffDefaults.CashierId)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Null_catalog_hires_nobody()
    {
        _rig.Staff.Catalog = null;
        _rig.Progression.AwardCheckout(LevelTwoPayment);
        _rig.Money.Add(StaffDefaults.CashierHireFee);
        int balance = _rig.Money.Balance;

        AssertThat(_rig.Staff.Hire(StaffDefaults.CashierId)).IsEqual(StaffResult.UnknownRole);

        AssertInt(_rig.Money.Balance).IsEqual(balance);
        AssertInt(_rig.Staff.HiredRoles.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Wage_line_is_charged_at_close_for_a_hired_cashier()
    {
        HireCashier();
        var day = OpenDay();
        DayReport? report = null;
        day.Closed += closed => report = closed;
        int before = _rig.Money.Balance;

        day.CloseShop();

        AssertBool(report != null).IsTrue();
        var wage = report!.Settlement.Lines.Single(line => line.Name == "Cashier wage");
        AssertInt(wage.Amount).IsEqual(StaffDefaults.CashierDailyWage);
        AssertInt(report.Settlement.Paid).IsEqual(report.Settlement.Total);
        AssertInt(_rig.Money.Balance).IsEqual(before - report.Settlement.Paid);
        AssertInt(day.Debt).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Short_balance_leaves_the_unpaid_wage_as_debt()
    {
        HireCashier();
        var day = OpenDay();
        DayReport? report = null;
        day.Closed += closed => report = closed;
        _rig.Money.TrySpend(_rig.Money.Balance);
        _rig.Money.Add(ShortBalance);

        day.CloseShop();

        AssertBool(report != null).IsTrue();
        AssertInt(report!.Settlement.Paid).IsEqual(ShortBalance);
        AssertInt(report.Settlement.Unpaid).IsEqual(report.Settlement.Total - ShortBalance);
        AssertInt(day.Debt).IsEqual(report.Settlement.Unpaid);
        AssertInt(_rig.Money.Balance).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Save_written_mid_checkout_reloads_hired_idle_and_closed()
    {
        HireCashier();
        var day = OpenDay();
        TestServices.Container.RegisterSingleton<IShopDayService>(day);
        TestServices.ExecutePendingCallbacks();
        _rig.Counter.Money = _rig.Money;
        var customer = new FakeCheckoutCustomer(PaymentMethod.Card, 10, 20);
        _rig.Counter.Checkout(customer);
        for (var i = 0; i < ClaimSteps; i++)
            _rig.Cashier.Step(StepSeconds);
        AssertBool(_rig.Counter.IsClaimed).IsTrue();
        int balance = _rig.Money.Balance;
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        await SettleAsync();

        AssertBool(_second.Staff.IsHired(StaffDefaults.CashierId)).IsTrue();
        AssertBool(_second.Day.IsOpen).IsFalse();
        AssertBool(_second.Counter.IsClaimed).IsFalse();
        AssertBool(_second.Counter.Session == null && _second.Counter.Front == null).IsTrue();
        AssertInt(_second.Money.Balance).IsEqual(balance);
        AssertInt(customer.LeaveCount).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Hired_restocker_survives_save_and_reload_at_home_with_empty_hands()
    {
        HireCashier();
        HireRestocker();
        var home = _rig.Restocker.GlobalPosition;
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        await SettleAsync();

        AssertBool(_second.Staff.IsHired(StaffDefaults.RestockerId)).IsTrue();
        AssertBool(_second.Restocker.Visible).IsTrue();
        AssertThat(_second.Restocker.GlobalPosition).IsEqual(home);
        AssertInt(_second.Restocker.GetNode("Hand").GetChildCount()).IsEqual(0);
        AssertThat(_second.Staff.HiredRoles.Select(role => role.Id).First()).IsEqual(StaffDefaults.CashierId);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Pack_carried_at_save_time_reloads_once_as_a_loose_container()
    {
        HireRestocker();
        var day = OpenDay();
        TestServices.Container.RegisterSingleton<IShopDayService>(day);
        TestServices.ExecutePendingCallbacks();
        var rackSlot = _rig.Shop.GetNode<CardShelf>("World/RightStorage/StorageRack").Slots.First();
        var pack = GD.Load<PackedScene>("res://Scenes/Shop/Items/Pack.tscn").Instantiate<CardContainer>();
        _rig.Shop.GetNode("World").AddChild(pack);
        AssertBool(rackSlot.TryLock(pack)).IsTrue();
        var carried = await PollFrames(() => pack.GetParent()?.Name.ToString() == "Hand");
        AssertBool(carried).IsTrue();
        var before = PackCount();
        AssertBool(_rig.Saves.ManualSave()).IsTrue();
        _rig.Close();

        _second = await ShopExpansionRig.StartAsync(_files);
        await SettleAsync();

        AssertInt(PackCount()).IsEqual(before);
        AssertInt(_second.Restocker.GetNode("Hand").GetChildCount()).IsEqual(0);
        AssertBool(PackContainers().All(c => c.GetParent()?.Name.ToString() != "Hand")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Saved_roster_with_an_unknown_role_or_without_the_restocker_hires_no_restocker()
    {
        var log = new RecordingLog();
        using var sink = Capture(log);
        var unknown = new NodeSave();
        unknown.SetOrAddProperty(HiredKey, "[\"cashier\",\"ghost\"]");
        var omitted = new NodeSave();
        omitted.SetOrAddProperty(HiredKey, "[\"cashier\"]");

        _rig.Staff.Load(unknown);
        AssertInt(log.Warnings.Count).IsEqual(1);
        AssertBool(_rig.Staff.IsHired(StaffDefaults.RestockerId)).IsFalse();
        _rig.Staff.Load(omitted);

        AssertInt(log.Warnings.Count).IsEqual(1);
        AssertBool(_rig.Staff.IsHired(StaffDefaults.RestockerId)).IsFalse();
        AssertBool(_rig.Staff.IsHired(StaffDefaults.CashierId)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Both_wages_are_charged_once_and_a_dismissed_restocker_stops_working()
    {
        HireCashier();
        HireRestocker();
        var day = OpenDay();
        TestServices.Container.RegisterSingleton<IShopDayService>(day);
        TestServices.ExecutePendingCallbacks();
        AssertThat(_rig.Staff.Dismiss(StaffDefaults.RestockerId)).IsEqual(StaffResult.Dismissed);
        var rackSlot = _rig.Shop.GetNode<CardShelf>("World/RightStorage/StorageRack").Slots.First();
        var pack = GD.Load<PackedScene>("res://Scenes/Shop/Items/Pack.tscn").Instantiate<CardContainer>();
        _rig.Shop.GetNode("World").AddChild(pack);
        AssertBool(rackSlot.TryLock(pack)).IsTrue();

        for (var i = 0; i < IdleFrames; i++)
            await ISceneRunner.SyncPhysicsFrame;

        AssertBool(_rig.Restocker.Visible).IsFalse();
        AssertBool(rackSlot.Cards.Contains(pack)).IsTrue();
        _rig.Money.Add(StaffDefaults.RestockerHireFee);
        AssertThat(_rig.Staff.Hire(StaffDefaults.RestockerId)).IsEqual(StaffResult.Hired);
        DayReport? report = null;
        day.Closed += closed => report = closed;
        day.CloseShop();
        AssertBool(report != null).IsTrue();
        AssertInt(report!.Settlement.Lines.Count(line => line.Name == "Cashier wage")).IsEqual(1);
        AssertInt(report.Settlement.Lines.Count(line => line.Name == "Restocker wage")).IsEqual(1);
    }

    // Reaches level 2 and leaves SpareMoney after the fee, so a reload shows whether the fee was charged again.
    private void HireCashier()
    {
        _rig.Money.TrySpend(_rig.Money.Balance);
        _rig.Money.Add(StaffDefaults.CashierHireFee + SpareMoney);
        _rig.Progression.AwardCheckout(LevelTwoPayment);
        AssertThat(_rig.Staff.Hire(StaffDefaults.CashierId)).IsEqual(StaffResult.Hired);
    }

    // Adds the restocker on top of whatever is hired: level 3 and the fee on top of the balance.
    private void HireRestocker()
    {
        _rig.Money.Add(StaffDefaults.RestockerHireFee);
        _rig.Progression.AwardCheckout(LevelThreePayment);
        AssertThat(_rig.Staff.Hire(StaffDefaults.RestockerId)).IsEqual(StaffResult.Hired);
    }

    private IEnumerable<CardContainer> PackContainers() =>
        _second!.Shop.GetTree().GetNodesInGroup("card_containers").OfType<CardContainer>()
            .Where(c => c.Kind == CardContainerKind.Pack && !c.IsOpened);

    private int PackCount() => (_second?.Shop ?? _rig.Shop).GetTree().GetNodesInGroup("card_containers")
        .OfType<CardContainer>().Count(c => c.Kind == CardContainerKind.Pack && !c.IsOpened);

    private static async Task<bool> PollFrames(Func<bool> done)
    {
        for (var frame = 0; frame < MaxFrames && !done(); frame++)
            await ISceneRunner.SyncPhysicsFrame;

        return done();
    }

    // The scene's day service gets the bill book a root would build and a save command that writes no file.
    private ShopDayService OpenDay()
    {
        var day = _rig.Day;
        day.Money = _rig.Money;
        day.Bills = new DailyBillBook(new IDailyBillSource[]
        {
            new RentBillSource(), new StaffWageBillSource(_rig.Staff),
        });
        day.SaveCommand = new NoOpSaveGame();
        // Opening the shop wakes the real spawner; it gets a random source and no frame loop, so it adds no customer.
        _rig.Spawner.Rng = RandomSources.Create(401UL);
        _rig.Spawner.ProcessMode = Node.ProcessModeEnum.Disabled;
        day.OpenShop();
        return day;
    }

    private static async Task SettleAsync()
    {
        for (var i = 0; i < SettleRounds; i++)
            await ShopExpansionRig.SettleAsync();
    }

    private static LogCapture Capture(ILog log) => new(log);

    private sealed class LogCapture : IDisposable
    {
        private readonly ILog? _previous = ILog.Sink;

        public LogCapture(ILog log) => ILog.Sink = log;

        public void Dispose() => ILog.Sink = _previous;
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Warnings { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message) => Warnings.Add(message);

        public void LogError(string message)
        {
        }

        public void LogError(string message, Exception exception)
        {
        }
    }
}
