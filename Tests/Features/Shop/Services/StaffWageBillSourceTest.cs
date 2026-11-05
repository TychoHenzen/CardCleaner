using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class StaffWageBillSourceTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void No_one_hired_gives_no_lines()
    {
        var source = new StaffWageBillSource(new FakeStaffService(StaffDefaults.Cashier()));

        AssertThat(source.GetBillLines(1).Count()).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Cashier_hired_gives_one_Cashier_wage_line_with_the_role_wage()
    {
        var staff = new FakeStaffService(StaffDefaults.Cashier());
        staff.HiredIds.Add(StaffDefaults.CashierId);
        var source = new StaffWageBillSource(staff);

        var lines = source.GetBillLines(1).ToList();

        AssertThat(lines.Count).IsEqual(1);
        AssertThat(lines[0].Name).IsEqual("Cashier wage");
        AssertThat(lines[0].Amount).IsEqual(StaffDefaults.CashierDailyWage);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Dismissed_gives_no_line()
    {
        var staff = new FakeStaffService(StaffDefaults.Cashier());
        staff.Hire(StaffDefaults.CashierId);
        staff.Dismiss(StaffDefaults.CashierId);
        var source = new StaffWageBillSource(staff);

        AssertThat(source.GetBillLines(1).Count()).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Day_does_not_change_the_wage()
    {
        var staff = new FakeStaffService(StaffDefaults.Cashier());
        staff.HiredIds.Add(StaffDefaults.CashierId);
        var source = new StaffWageBillSource(staff);

        AssertThat(source.GetBillLines(1).Single().Amount).IsEqual(StaffDefaults.CashierDailyWage);
        AssertThat(source.GetBillLines(5).Single().Amount).IsEqual(StaffDefaults.CashierDailyWage);
    }
}
