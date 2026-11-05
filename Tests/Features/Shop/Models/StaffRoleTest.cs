using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class StaffRoleTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void Cashier_defaults_match_constants()
    {
        var cashier = StaffDefaults.Cashier();

        AssertThat(cashier.Id).IsEqual(StaffDefaults.CashierId);
        AssertThat(cashier.Id).IsEqual("cashier");
        AssertThat(cashier.UnlockId).IsEqual(StaffDefaults.CashierUnlockId);
        AssertThat(cashier.UnlockId).IsEqual("staff_cashier");
        AssertThat(cashier.DisplayName).IsEqual(StaffDefaults.CashierName);
        AssertThat(cashier.HireFee).IsEqual(StaffDefaults.CashierHireFee);
        AssertThat(cashier.HireFee).IsEqual(200);
        AssertThat(cashier.DailyWage).IsEqual(StaffDefaults.CashierDailyWage);
        AssertThat(cashier.DailyWage).IsEqual(40);
        AssertThat(cashier.SecondsPerScan).IsEqual(StaffDefaults.CashierSecondsPerScan);
        AssertThat(cashier.SecondsPerScan).IsEqual(1.5f);
        AssertThat(cashier.SecondsPerPayment).IsEqual(StaffDefaults.CashierSecondsPerPayment);
        AssertThat(cashier.SecondsPerPayment).IsEqual(2.0f);
        AssertThat(cashier.TakeOverDelay).IsEqual(StaffDefaults.CashierTakeOverDelay);
        AssertThat(cashier.TakeOverDelay).IsEqual(3.0f);
    }
}
