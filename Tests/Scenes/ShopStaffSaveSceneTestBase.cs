using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.SaveSlots.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Scenes;

/// <summary>
///     The shop scene started on an in-memory slot for the staff save suites: the rig, the hiring helpers, the day
///     service the way a root would build it, and a capture for the warnings of a load.
/// </summary>
[RequireGodotRuntime]
public abstract class ShopStaffSaveSceneTestBase
{
    private protected const string HiredKey = "hired";
    private protected const int SpareMoney = 137;
    private protected const int LevelTwoPayment = 500;
    private protected const int LevelThreePayment = 1500;
    private const int SettleRounds = 3;

    private protected InMemorySlotFileSystem _files = null!;
    private protected ShopExpansionRig _rig = null!;
    private protected ShopExpansionRig? _second;

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

    // Reaches level 2 and leaves SpareMoney after the fee, so a reload shows whether the fee was charged again.
    private protected void HireCashier()
    {
        _rig.Money.TrySpend(_rig.Money.Balance);
        _rig.Money.Add(StaffDefaults.CashierHireFee + SpareMoney);
        _rig.Progression.AwardCheckout(LevelTwoPayment);
        AssertThat(_rig.Staff.Hire(StaffDefaults.CashierId)).IsEqual(StaffResult.Hired);
    }

    // Adds the restocker on top of whatever is hired: level 3 and the fee on top of the balance.
    private protected void HireRestocker()
    {
        _rig.Money.Add(StaffDefaults.RestockerHireFee);
        _rig.Progression.AwardCheckout(LevelThreePayment);
        AssertThat(_rig.Staff.Hire(StaffDefaults.RestockerId)).IsEqual(StaffResult.Hired);
    }

    // Opens the day and registers it, as the shop root does, so the staff see the day service.
    private protected ShopDayService OpenRegisteredDay()
    {
        var day = OpenDay();
        TestServices.Container.RegisterSingleton<IShopDayService>(day);
        TestServices.ExecutePendingCallbacks();
        return day;
    }

    // The scene's day service gets the bill book a root would build and a save command that writes no file.
    private protected ShopDayService OpenDay()
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

    private protected static async Task SettleAsync()
    {
        for (var i = 0; i < SettleRounds; i++)
            await ShopExpansionRig.SettleAsync();
    }

    private protected static LogCapture Capture(ILog log) => new(log);

    private protected sealed class LogCapture : IDisposable
    {
        private readonly ILog? _previous = ILog.Sink;

        public LogCapture(ILog log) => ILog.Sink = log;

        public void Dispose() => ILog.Sink = _previous;
    }

    private protected sealed class RecordingLog : ILog
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
