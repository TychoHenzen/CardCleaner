using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class DailyBillsTest
{
    private static readonly IReadOnlyList<BillLine> TwoBills =
    [
        new BillLine("Rent", 100),
        new BillLine("Power", 50),
    ];

    [TestCase]
    [TestCategory("Unit")]
    public static void BalanceCoversBills()
    {
        var settlement = DailyBills.Settle(TwoBills, 500);

        AssertThat(settlement.Paid).IsEqual(150);
        AssertThat(settlement.Unpaid).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ShortBalanceLeavesUnpaid()
    {
        var settlement = DailyBills.Settle(TwoBills, 120);

        AssertThat(settlement.Paid).IsEqual(120);
        AssertThat(settlement.Unpaid).IsEqual(30);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NegativeAmountsAreDropped()
    {
        IReadOnlyList<BillLine> lines =
        [
            new BillLine("Rent", 100),
            new BillLine("Refund", -40),
            new BillLine("Nothing", 0),
        ];

        var settlement = DailyBills.Settle(lines, 500);

        AssertThat(settlement.Total).IsEqual(100);
        AssertThat(settlement.Lines.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ZeroBalancePaysNothing()
    {
        var settlement = DailyBills.Settle(TwoBills, 0);

        AssertThat(settlement.Paid).IsEqual(0);
        AssertThat(settlement.Unpaid).IsEqual(150);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CarriedDebtIsPartOfTotal()
    {
        IReadOnlyList<BillLine> lines = [new BillLine("Rent", 40)];

        var settlement = DailyBills.Settle(lines, 500, 30);

        AssertThat(settlement.CarriedIn).IsEqual(30);
        AssertThat(settlement.Total).IsEqual(70);
        AssertThat(settlement.Paid).IsEqual(70);
        AssertThat(settlement.Unpaid).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ShortBalanceWithDebtCarriesMore()
    {
        IReadOnlyList<BillLine> lines = [new BillLine("Rent", 40)];

        var settlement = DailyBills.Settle(lines, 50, 30);

        AssertThat(settlement.Paid).IsEqual(50);
        AssertThat(settlement.Unpaid).IsEqual(20);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NegativeDebtCountsAsZero()
    {
        IReadOnlyList<BillLine> lines = [new BillLine("Rent", 40)];

        var settlement = DailyBills.Settle(lines, 500, -25);

        AssertThat(settlement.CarriedIn).IsEqual(0);
        AssertThat(settlement.Total).IsEqual(40);
    }
}
