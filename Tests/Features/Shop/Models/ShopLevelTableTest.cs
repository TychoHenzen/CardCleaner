using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class ShopLevelTableTest
{
    [TestCase(0, 1)]
    [TestCase(49, 1)]
    [TestCase(50, 2)]
    [TestCase(149, 2)]
    [TestCase(150, 3)]
    [TestCase(499, 4)]
    [TestCase(500, 5)]
    [TestCase(100000, 5)]
    [TestCase(-7, 1)]
    [TestCategory("Unit")]
    public static void LevelForMapsXpToTheLevelItReaches(int xp, int expectedLevel)
    {
        AssertThat(ShopLevelTable.Default.LevelFor(xp)).IsEqual(expectedLevel);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void XpIntoLevelAndSpanDescribeTheCurrentLevel()
    {
        var table = ShopLevelTable.Default;

        AssertThat(table.XpIntoLevel(70)).IsEqual(20);
        AssertThat(table.XpForNextLevel(70)).IsEqual(100);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TopLevelHasNoNextLevel()
    {
        var table = ShopLevelTable.Default;

        AssertThat(table.MaxLevel).IsEqual(5);
        AssertThat(table.XpForNextLevel(500)).IsNull();
        AssertThat(table.XpIntoLevel(100000)).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ProgressIsTheEarnedFractionOfTheLevel()
    {
        var table = ShopLevelTable.Default;

        AssertThat(table.Progress(25)).IsEqual(0.5f);
        AssertThat(table.Progress(500)).IsEqual(1.0f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyThresholdsThrow()
    {
        AssertThrown(() => new ShopLevelTable(Array.Empty<int>())).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void FirstThresholdMustBeZero()
    {
        AssertThrown(() => new ShopLevelTable(new[] { 1, 5 })).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ThresholdsMustBeStrictlyIncreasingWithNoRepeat()
    {
        AssertThrown(() => new ShopLevelTable(new[] { 0, 5, 5 })).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ThresholdsMustNeverDecrease()
    {
        AssertThrown(() => new ShopLevelTable(new[] { 0, 9, 3 })).IsInstanceOf<ArgumentException>();
    }
}
