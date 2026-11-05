using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class StaffRosterTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Decide_below_unlock_is_Locked()
    {
        var roster = new StaffRoster();

        AssertThat(roster.Decide(StaffDefaults.Cashier(), false, 10000)).IsEqual(StaffResult.Locked);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Decide_short_balance_is_CannotAfford()
    {
        var roster = new StaffRoster();
        var fee = StaffDefaults.Cashier().HireFee;

        AssertThat(roster.Decide(StaffDefaults.Cashier(), true, fee - 1)).IsEqual(StaffResult.CannotAfford);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Decide_hired_role_is_AlreadyHired()
    {
        var roster = new StaffRoster();
        roster.Add(StaffDefaults.CashierId);

        AssertThat(roster.Decide(StaffDefaults.Cashier(), true, 10000)).IsEqual(StaffResult.AlreadyHired);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Decide_ok_is_Hired_and_does_not_add()
    {
        var roster = new StaffRoster();

        AssertThat(roster.Decide(StaffDefaults.Cashier(), true, StaffDefaults.CashierHireFee))
            .IsEqual(StaffResult.Hired);
        AssertThat(roster.IsHired(StaffDefaults.CashierId)).IsFalse();
        AssertThat(roster.HiredRoleIds.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Decide_null_role_is_UnknownRole()
    {
        var roster = new StaffRoster();

        AssertThat(roster.Decide(null, true, 10000)).IsEqual(StaffResult.UnknownRole);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Dismiss_hired_is_Dismissed_and_removes()
    {
        var roster = new StaffRoster();
        roster.Add("a");
        roster.Add("b");

        AssertThat(roster.Dismiss("a")).IsEqual(StaffResult.Dismissed);
        AssertThat(roster.IsHired("a")).IsFalse();
        AssertThat(string.Join(",", roster.HiredRoleIds)).IsEqual("b");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Dismiss_absent_is_NotHired()
    {
        var roster = new StaffRoster();

        AssertThat(roster.Dismiss("missing")).IsEqual(StaffResult.NotHired);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Restore_drops_unknown_and_duplicate_ids_and_returns_the_dropped_count()
    {
        var roster = new StaffRoster();
        roster.Add("stale");

        var dropped = roster.Restore(new[] { "a", "x", "b", "a" }, id => id != "x");

        AssertThat(dropped).IsEqual(2);
        AssertThat(string.Join(",", roster.HiredRoleIds)).IsEqual("a,b");
        AssertThat(roster.IsHired("stale")).IsFalse();
    }
}
