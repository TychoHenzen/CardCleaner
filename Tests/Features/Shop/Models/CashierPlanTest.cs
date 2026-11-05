using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class CashierPlanTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Idle_plan_ignores_time()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());

        AssertThat(plan.Advance(100f)).IsEqual(CashierAction.None);
        AssertThat(plan.Step).IsEqual(CashierStep.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Waits_take_over_delay_before_the_first_scan()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());
        plan.Begin(3);

        AssertThat(plan.Advance(2.9f)).IsEqual(CashierAction.None);
        AssertThat(plan.Advance(0.1f)).IsEqual(CashierAction.None);
        AssertThat(plan.Step).IsEqual(CashierStep.Scanning);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Scans_one_item_per_interval_in_order()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());
        plan.Begin(3);
        plan.Advance(3f);

        AssertThat(plan.Advance(1.0f)).IsEqual(CashierAction.None);
        AssertThat(plan.Advance(0.5f)).IsEqual(CashierAction.Scan);
        AssertThat(plan.ScanIndex).IsEqual(0);
        AssertThat(plan.Advance(1.0f)).IsEqual(CashierAction.None);
        AssertThat(plan.Advance(0.5f)).IsEqual(CashierAction.Scan);
        AssertThat(plan.ScanIndex).IsEqual(1);
        AssertThat(plan.Advance(1.0f)).IsEqual(CashierAction.None);
        AssertThat(plan.Advance(0.5f)).IsEqual(CashierAction.Scan);
        AssertThat(plan.ScanIndex).IsEqual(2);
        AssertThat(plan.Step).IsEqual(CashierStep.Paying);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Pays_after_the_last_scan_and_the_payment_delay()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());
        plan.Begin(1);
        plan.Advance(3f);
        AssertThat(plan.Advance(1.5f)).IsEqual(CashierAction.Scan);
        AssertThat(plan.Step).IsEqual(CashierStep.Paying);

        AssertThat(plan.Advance(1.0f)).IsEqual(CashierAction.None);
        AssertThat(plan.Advance(1.0f)).IsEqual(CashierAction.Pay);
        AssertThat(plan.Step).IsEqual(CashierStep.Done);
        AssertThat(plan.Advance(1.0f)).IsEqual(CashierAction.None);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Done_returns_None_for_more_time()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());
        plan.Begin(1);
        plan.Advance(3f);
        plan.Advance(1.5f);
        plan.Advance(2.0f);

        AssertThat(plan.Step).IsEqual(CashierStep.Done);
        AssertThat(plan.Advance(100f)).IsEqual(CashierAction.None);
        AssertThat(plan.Step).IsEqual(CashierStep.Done);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Zero_or_negative_delta_changes_nothing()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());
        plan.Begin(2);
        plan.Advance(2.9f);

        AssertThat(plan.Advance(0f)).IsEqual(CashierAction.None);
        AssertThat(plan.Advance(-5f)).IsEqual(CashierAction.None);
        AssertThat(plan.Step).IsEqual(CashierStep.Claiming);

        AssertThat(plan.Advance(0.1f)).IsEqual(CashierAction.None);
        AssertThat(plan.Step).IsEqual(CashierStep.Scanning);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Huge_delta_yields_one_action_per_call()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());
        plan.Begin(2);

        AssertThat(plan.Advance(1000f)).IsEqual(CashierAction.None);
        AssertThat(plan.Step).IsEqual(CashierStep.Scanning);

        AssertThat(plan.Advance(1000f)).IsEqual(CashierAction.Scan);
        AssertThat(plan.ScanIndex).IsEqual(0);
        AssertThat(plan.Step).IsEqual(CashierStep.Scanning);

        AssertThat(plan.Advance(1000f)).IsEqual(CashierAction.Scan);
        AssertThat(plan.ScanIndex).IsEqual(1);
        AssertThat(plan.Step).IsEqual(CashierStep.Paying);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Reset_returns_to_Idle()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());
        plan.Begin(1);
        plan.Advance(3f);

        plan.Reset();

        AssertThat(plan.Step).IsEqual(CashierStep.Idle);
        AssertThat(plan.Advance(100f)).IsEqual(CashierAction.None);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Begin_with_no_items_throws()
    {
        var plan = new CashierPlan(StaffDefaults.Cashier());

        AssertThrown(() => plan.Begin(0)).IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
