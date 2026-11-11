using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Tests.Features.Shop.Services;

/// <summary>Hiring and dismissing through the staff service: fees, unlock levels, refusals and events.</summary>
[TestSuite]
[RequireGodotRuntime]
public class StaffServiceTest : StaffServiceTestBase
{
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
    public void Null_catalog_hires_nothing()
    {
        var empty = AutoFree(new StaffService { Money = _money, Progression = new OpenProgression() });

        AssertThat(empty.Hire(CashierId)).IsEqual(StaffResult.UnknownRole);
        AssertThat(empty.HiredRoles.Count).IsEqual(0);
        AssertThat(empty.Roles.Count).IsEqual(0);
        AssertThat(_money.Balance).IsEqual(StartingBalance);
    }
}
