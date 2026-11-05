using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class DailyBillBookTest
{
    private const int RentAmount = 40;
    private const int WageAmount = 25;

    [TestCase]
    [TestCategory("Unit")]
    public static void LinesFollowConstructorOrderAndTotalSums()
    {
        var book = new DailyBillBook(new IDailyBillSource[]
        {
            new FakeSource(new BillLine("Rent", RentAmount)),
            new FakeSource(new BillLine("Wage", WageAmount)),
        });

        var names = book.LinesFor(1).Select(line => line.Name).ToArray();

        AssertThat(string.Join(",", names)).IsEqual("Rent,Wage");
        AssertThat(book.TotalFor(1)).IsEqual(RentAmount + WageAmount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptySourceContributesNothing()
    {
        var book = new DailyBillBook(new IDailyBillSource[] { new FakeSource() });

        AssertThat(book.LinesFor(1).Count).IsEqual(0);
        AssertThat(book.TotalFor(1)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NonPositiveLinesAreDropped()
    {
        var book = new DailyBillBook(new IDailyBillSource[]
        {
            new FakeSource(new BillLine("Rent", RentAmount), new BillLine("Zero", 0), new BillLine("Credit", -5)),
        });

        var names = book.LinesFor(1).Select(line => line.Name).ToArray();

        AssertThat(string.Join(",", names)).IsEqual("Rent");
        AssertThat(book.TotalFor(1)).IsEqual(RentAmount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PassesTheDayToEachSource()
    {
        var source = new FakeSource(new BillLine("Rent", RentAmount));
        var book = new DailyBillBook(new IDailyBillSource[] { source });

        book.LinesFor(3);

        AssertThat(source.Days.Count).IsEqual(1);
        AssertThat(source.Days[0]).IsEqual(3);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScanBuildsTheBookWithRent()
    {
        var container = new ServiceContainer();
        container.RegisterSingleton(typeof(IShopExpansion), new NoLotsExpansion());
        container.RegisterSingleton(typeof(IStaffService), new FakeStaffService());
        TestServices.RegisterGameServiceClasses(container);

        var book = container.Resolve<IDailyBillBook>();

        var lines = book.LinesFor(1);
        AssertThat(lines.Count).IsEqual(1);
        AssertThat(lines[0].Name).IsEqual("Rent");
        AssertThat(lines[0].Amount).IsEqual(RentAmount);
        AssertThat(book.TotalFor(1)).IsEqual(RentAmount);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScanBuildsTheBookWithTheCashierWage()
    {
        var container = new ServiceContainer();
        container.RegisterSingleton(typeof(IShopExpansion), new NoLotsExpansion());
        var staff = new FakeStaffService(StaffDefaults.Cashier());
        staff.HiredIds.Add(StaffDefaults.CashierId);
        container.RegisterSingleton(typeof(IStaffService), staff);
        TestServices.RegisterGameServiceClasses(container);

        var book = container.Resolve<IDailyBillBook>();

        var lines = book.LinesFor(1);
        AssertThat(lines.Count).IsEqual(2);
        AssertThat(lines[0].Name).IsEqual("Rent");
        AssertThat(lines[1].Name).IsEqual("Cashier wage");
        AssertThat(lines[1].Amount).IsEqual(StaffDefaults.CashierDailyWage);
        AssertThat(book.TotalFor(1)).IsEqual(RentAmount + StaffDefaults.CashierDailyWage);
    }

    private sealed class NoLotsExpansion : IShopExpansion
    {
        public ExpansionPlan Plan { get; } = new([], new HashSet<string>());

        public event System.Action<string>? Opened
        {
            add { }
            remove { }
        }

        public int ExtraCustomersPerDay => 0;

        public PurchaseOutcome TryBuy(string lotId) => PurchaseOutcome.Unknown;
    }

    private sealed class FakeSource : IDailyBillSource
    {
        private readonly IReadOnlyList<BillLine> _lines;

        public FakeSource(params BillLine[] lines)
        {
            _lines = lines;
        }

        public List<int> Days { get; } = new();

        public IEnumerable<BillLine> GetBillLines(int day)
        {
            Days.Add(day);
            return _lines;
        }
    }
}
