using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Saveable;

namespace CardCleaner.Tests.Features.Shop.Services;

/// <summary>The hired roster through the staff service's save and load, and the warnings of a bad save.</summary>
[TestSuite]
[RequireGodotRuntime]
public class StaffServiceSaveTest : StaffServiceTestBase
{
    [TestCase]
    [TestCategory("Unit")]
    public void Save_Load_round_trips_the_hired_set()
    {
        _staff.Hire(CashierId);
        var save = new NodeSave();
        _staff.Save(save);

        var reloaded = NewStaffService();
        reloaded.Load(save);

        AssertBool(reloaded.IsHired(CashierId)).IsTrue();
        AssertThat(reloaded.HiredRoles.Count).IsEqual(1);
        AssertThat(_money.Balance).IsEqual(StartingBalance - CashierFee);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_with_unknown_and_duplicate_ids_keeps_valid_ones()
    {
        var save = new NodeSave();
        save.SetOrAddProperty("hired", @"[""cashier"",""ghost"",""cashier""]");

        var warnings = LoadAndCountWarnings(save);

        AssertBool(_staff.IsHired(CashierId)).IsTrue();
        AssertThat(_staff.HiredRoles.Count).IsEqual(1);
        AssertThat(warnings).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_with_unreadable_json_changes_nothing()
    {
        _staff.Hire(CashierId);
        var save = new NodeSave();
        save.SetOrAddProperty("hired", "not json");

        var warnings = LoadAndCountWarnings(save);

        AssertBool(_staff.IsHired(CashierId)).IsTrue();
        AssertThat(warnings).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void Load_with_a_non_string_hired_value_changes_nothing()
    {
        _staff.Hire(CashierId);
        // A hand-edited save may hold the list itself, not the string that Save writes.
        var save = new NodeSave();
        save.SetOrAddProperty("hired", new List<string> { CashierId });

        var warnings = LoadAndCountWarnings(save);

        AssertBool(_staff.IsHired(CashierId)).IsTrue();
        AssertThat(warnings).IsEqual(1);
    }

    // Loads the save with a recording log installed, and returns how many warnings the load logged.
    private int LoadAndCountWarnings(NodeSave save)
    {
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            _staff.Load(save);
        }
        finally
        {
            ILog.Sink = previous;
        }

        return log.Warnings.Count;
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
