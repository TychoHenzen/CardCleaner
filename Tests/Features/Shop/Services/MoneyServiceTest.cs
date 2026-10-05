using System.Collections.Generic;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class MoneyServiceTest
{
    private MoneyService _money = null!;

    [BeforeTest]
    public void Setup()
    {
        _money = new MoneyService { StartingBalance = 100 };
    }

    [AfterTest]
    public void Teardown()
    {
        _money.Free();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BalanceStartsAtTheExportedStartingBalance()
    {
        AssertThat(_money.Balance).IsEqual(100);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TrySpendDeductsWhenAffordable()
    {
        AssertBool(_money.TrySpend(40)).IsTrue();
        AssertThat(_money.Balance).IsEqual(60);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TrySpendSucceedsForTheExactBalance()
    {
        AssertBool(_money.TrySpend(100)).IsTrue();
        AssertThat(_money.Balance).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TrySpendRefusesWhenUnaffordableAndKeepsTheBalance()
    {
        AssertBool(_money.TrySpend(101)).IsFalse();
        AssertThat(_money.Balance).IsEqual(100);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TrySpendRefusesZeroAndNegativeAmounts()
    {
        AssertBool(_money.TrySpend(0)).IsFalse();
        AssertBool(_money.TrySpend(-5)).IsFalse();
        AssertThat(_money.Balance).IsEqual(100);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AddIncreasesTheBalanceAndIgnoresNonPositiveAmounts()
    {
        _money.Add(25);
        _money.Add(-10);
        _money.Add(0);

        AssertThat(_money.Balance).IsEqual(125);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void BalanceChangedReportsTheNewBalanceOnlyOnRealChanges()
    {
        var seen = new List<int>();
        _money.BalanceChanged += seen.Add;

        _money.TrySpend(30);
        _money.TrySpend(1000);
        _money.Add(5);

        AssertThat(seen.Count).IsEqual(2);
        AssertThat(seen[0]).IsEqual(70);
        AssertThat(seen[1]).IsEqual(75);
    }
}
