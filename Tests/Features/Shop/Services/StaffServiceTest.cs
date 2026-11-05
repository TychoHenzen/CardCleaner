using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class StaffServiceTest
{
    private const int StartingBalance = 1000;
    private const string CashierId = "cashier";
    private const string CashierUnlock = "staff_cashier";
    private const int CashierFee = 200;

    private Node3D _world = null!;
    private MoneyService _money = null!;
    private StaffService _staff = null!;

    [BeforeTest]
    public void Setup()
    {
        _world = new Node3D();
        AddNode(_world);

        _money = new MoneyService { StartingBalance = StartingBalance };
        _world.AddChild(_money);

        _staff = AutoFree(new StaffService
        {
            Catalog = CashierCatalog(),
            Money = _money,
            Progression = new OpenProgression(),
        });
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Hire_locked_does_not_charge()
    {
        _staff.Progression = new OpenProgression(CashierUnlock);

        AssertThat(_staff.Hire(CashierId)).IsEqual(StaffResult.Locked);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
        AssertBool(_staff.IsHired(CashierId)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Hire_ok_charges_the_fee_once_and_raises_Changed_once()
    {
        var changes = 0;
        _staff.Changed += () => changes++;

        AssertThat(_staff.Hire(CashierId)).IsEqual(StaffResult.Hired);
        AssertThat(_money.Balance).IsEqual(StartingBalance - CashierFee);
        AssertBool(_staff.IsHired(CashierId)).IsTrue();
        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Hire_unaffordable_leaves_balance_and_roster_unchanged()
    {
        var poor = new MoneyService { StartingBalance = CashierFee - 1 };
        _world.AddChild(poor);
        _staff.Money = poor;
        var changes = 0;
        _staff.Changed += () => changes++;

        AssertThat(_staff.Hire(CashierId)).IsEqual(StaffResult.CannotAfford);
        AssertThat(poor.Balance).IsEqual(CashierFee - 1);
        AssertBool(_staff.IsHired(CashierId)).IsFalse();
        AssertThat(changes).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Hire_twice_is_AlreadyHired_and_charges_once()
    {
        _staff.Hire(CashierId);

        AssertThat(_staff.Hire(CashierId)).IsEqual(StaffResult.AlreadyHired);
        AssertThat(_money.Balance).IsEqual(StartingBalance - CashierFee);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Restocker_is_hired_at_level_3_refused_at_level_2_and_not_hired_twice()
    {
        var progression = AutoFree(new ShopProgression());
        _staff.Catalog = CashierAndRestockerCatalog();
        _staff.Progression = progression;

        progression.AwardCheckout(500);
        AssertThat(_staff.Hire(StaffRoleIds.Restocker)).IsEqual(StaffResult.Locked);
        AssertThat(_money.Balance).IsEqual(StartingBalance);

        progression.AwardCheckout(1000);
        AssertThat(_staff.Hire(StaffRoleIds.Restocker)).IsEqual(StaffResult.Hired);
        AssertThat(_money.Balance).IsEqual(StartingBalance - StaffDefaults.RestockerHireFee);
        AssertThat(_staff.Hire(StaffRoleIds.Restocker)).IsEqual(StaffResult.AlreadyHired);
        AssertThat(_money.Balance).IsEqual(StartingBalance - StaffDefaults.RestockerHireFee);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Hire_unknown_role_is_UnknownRole()
    {
        AssertThat(_staff.Hire("ghost")).IsEqual(StaffResult.UnknownRole);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Hire_with_no_progression_is_Locked()
    {
        _staff.Progression = null;

        AssertThat(_staff.Hire(CashierId)).IsEqual(StaffResult.Locked);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Dismiss_removes_the_role_and_refunds_nothing()
    {
        _staff.Hire(CashierId);
        var changes = 0;
        _staff.Changed += () => changes++;

        AssertThat(_staff.Dismiss(CashierId)).IsEqual(StaffResult.Dismissed);
        AssertBool(_staff.IsHired(CashierId)).IsFalse();
        AssertThat(_money.Balance).IsEqual(StartingBalance - CashierFee);
        AssertThat(changes).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Dismiss_unhired_is_NotHired()
    {
        var changes = 0;
        _staff.Changed += () => changes++;

        AssertThat(_staff.Dismiss(CashierId)).IsEqual(StaffResult.NotHired);
        AssertThat(changes).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Save_Load_round_trips_the_hired_set()
    {
        _staff.Hire(CashierId);
        var save = new NodeSave();
        _staff.Save(save);

        var reloaded = AutoFree(new StaffService
        {
            Catalog = CashierCatalog(),
            Money = _money,
            Progression = new OpenProgression(),
        });
        reloaded.Load(save);

        AssertBool(reloaded.IsHired(CashierId)).IsTrue();
        AssertThat(reloaded.HiredRoles.Count).IsEqual(1);
        AssertThat(_money.Balance).IsEqual(StartingBalance - CashierFee);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_with_unknown_and_duplicate_ids_keeps_valid_ones()
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var save = new NodeSave();
            save.SetOrAddProperty("hired", @"[""cashier"",""ghost"",""cashier""]");

            _staff.Load(save);

            AssertBool(_staff.IsHired(CashierId)).IsTrue();
            AssertThat(_staff.HiredRoles.Count).IsEqual(1);
            AssertThat(log.Warnings.Count).IsEqual(1);
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_with_unreadable_json_changes_nothing()
    {
        _staff.Hire(CashierId);
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            var save = new NodeSave();
            save.SetOrAddProperty("hired", "not json");

            _staff.Load(save);

            AssertBool(_staff.IsHired(CashierId)).IsTrue();
            AssertThat(log.Warnings.Count).IsEqual(1);
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_with_a_non_string_hired_value_changes_nothing()
    {
        _staff.Hire(CashierId);
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            // A hand-edited save may hold the list itself, not the string that Save writes.
            var save = new NodeSave();
            save.SetOrAddProperty("hired", new List<string> { CashierId });

            _staff.Load(save);

            AssertBool(_staff.IsHired(CashierId)).IsTrue();
            AssertThat(log.Warnings.Count).IsEqual(1);
        }
        finally
        {
            ILog.Sink = previous;
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Null_catalog_hires_nothing()
    {
        var empty = AutoFree(new StaffService { Money = _money, Progression = new OpenProgression() });

        AssertThat(empty.Hire(CashierId)).IsEqual(StaffResult.UnknownRole);
        AssertThat(empty.HiredRoles.Count).IsEqual(0);
        AssertThat(empty.Roles.Count).IsEqual(0);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
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

    private static StaffCatalog CashierAndRestockerCatalog() => new()
    {
        Roles =
        [
            CashierCatalog().Roles[0],
            new StaffRoleData
            {
                Id = StaffDefaults.RestockerId, DisplayName = StaffDefaults.RestockerName,
                UnlockId = StaffDefaults.RestockerUnlockId, HireFee = StaffDefaults.RestockerHireFee,
                DailyWage = StaffDefaults.RestockerDailyWage,
            },
        ],
    };

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
