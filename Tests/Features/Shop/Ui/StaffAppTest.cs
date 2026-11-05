using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Scripts.Features.Shop.Ui;
using CardCleaner.Tests.TestUtilities;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>
///     The Staff app over the real <see cref="StaffService"/>, so lock states, fees and the hire rules are the
///     production ones. Button presses run deferred, so each press waits one frame before it asserts.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StaffAppTest
{
    private const string CashierId = "cashier";
    private const string CashierUnlock = "staff_cashier";
    private const int CashierFee = 200;
    private const int StartingBalance = 1000;

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private StaffService _staff = null!;

    [AfterTest]
    public static void Teardown() => TestServices.Reset();

    [TestCase]
    [TestCategory("Unit")]
    public void Shows_cashier_card_with_fee_and_wage()
    {
        var app = Build(new OpenProgression());

        var texts = LabelTexts(app);
        AssertBool(texts.Contains("Cashier")).IsTrue();
        AssertBool(texts.Contains("Fee: 200")).IsTrue();
        AssertBool(texts.Contains("Wage: 40 per day")).IsTrue();
        AssertBool(app.FindChild("Hire", true, false) != null).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Locked_role_shows_level_and_disables_hire()
    {
        var app = Build(new OpenProgression(CashierUnlock));

        AssertBool(LabelTexts(app).Contains("Locked: level 2")).IsTrue();
        var hire = app.FindChild("Hire", true, false) as Button;
        AssertBool(hire != null).IsTrue();
        AssertBool(hire!.Disabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Hire_button_hires_and_updates_the_message()
    {
        var app = Build(new OpenProgression());

        app.FindChild("Hire", true, false)!.GetNode<Button>(".").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitForFrames(1);

        AssertBool(_staff.IsHired(CashierId)).IsTrue();
        AssertThat(_money.Balance).IsEqual(StartingBalance - CashierFee);
        AssertThat(MessageOf(app)).IsEqual("Hired Cashier.");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Dismiss_button_dismisses()
    {
        var app = Build(new OpenProgression());
        AssertThat(_staff.Hire(CashierId)).IsEqual(StaffResult.Hired);
        AssertBool(app.FindChild("Dismiss", true, false) != null).IsTrue();

        app.FindChild("Dismiss", true, false)!.GetNode<Button>(".").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitForFrames(1);

        AssertBool(_staff.IsHired(CashierId)).IsFalse();
        AssertThat(MessageOf(app)).IsEqual("Dismissed Cashier.");
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task Unaffordable_hire_shows_the_reason()
    {
        var app = Build(new OpenProgression(), balance: CashierFee - 1);

        app.FindChild("Hire", true, false)!.GetNode<Button>(".").EmitSignal(BaseButton.SignalName.Pressed);
        await WaitForFrames(1);

        AssertBool(_staff.IsHired(CashierId)).IsFalse();
        AssertThat(_money.Balance).IsEqual(CashierFee - 1);
        AssertThat(MessageOf(app)).IsEqual("Not enough money: 200 needed.");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Not_injected_logs_and_shows_nothing()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            // The composition root injects every IDependent that enters the tree, so the not-injected path is
            // reached by running _Ready on a node that never entered the tree.
            var app = AutoFree(new StaffApp { Name = "StaffApp" })!;
            app._Ready();

            AssertThat(log.Errors.Count).IsEqual(1);
            AssertBool(app.FindChild("Hire", true, false) == null).IsTrue();
            AssertThat(MessageOf(app)).IsEqual(string.Empty);
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    /// <summary>Builds the app inside the tree with the real services registered and injected.</summary>
    private StaffApp Build(OpenProgression progression, int balance = StartingBalance)
    {
        _world = new Node3D();
        AddNode(_world);

        _money = new MoneyService { StartingBalance = balance };
        _world.AddChild(_money);
        _staff = AutoFree(new StaffService
        {
            Catalog = CashierCatalog(),
            Money = _money,
            Progression = progression,
        });

        TestServices.Container.RegisterSingleton<IStaffService>(_staff);
        TestServices.Container.RegisterSingleton<IMoneyService>(_money);
        TestServices.Container.RegisterSingleton<IShopProgression>(progression);

        var app = new StaffApp { Name = "StaffApp" };
        TestServices.Inject(app);
        AddNode(app);
        return app;
    }

    private static StaffCatalog CashierCatalog() => new()
    {
        Roles =
        [
            new StaffRoleData
            {
                Id = CashierId, DisplayName = "Cashier", UnlockId = CashierUnlock, HireFee = CashierFee,
                DailyWage = 40, SecondsPerScan = 1.5f, SecondsPerPayment = 2f, TakeOverDelay = 3f,
            },
        ],
    };

    private static string MessageOf(Node app) => app.FindChild("Message", true, false) is Label label
        ? label.Text
        : string.Empty;

    private static List<string> LabelTexts(Node root)
    {
        var texts = new List<string>();
        CollectLabels(root, texts);
        return texts;
    }

    private static void CollectLabels(Node node, List<string> texts)
    {
        if (node is Label label)
            texts.Add(label.Text);
        foreach (var child in node.GetChildren())
            CollectLabels(child, texts);
    }

    private static async Task WaitForFrames(int count)
    {
        for (var index = 0; index < count; index++)
            await ISceneRunner.SyncProcessFrame;
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Warnings { get; } = new();
        public List<string> Errors { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message) => Warnings.Add(message);

        public void LogError(string message) => Errors.Add(message);

        public void LogError(string message, Exception exception) => Errors.Add(message);
    }
}
