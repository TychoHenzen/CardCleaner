using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class StockEntryTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void LockedWhenLevelBelowRequired()
    {
        var entry = new StockEntry("shelf", "Shelf", 10, 3);

        AssertThat(entry.IsLocked(1)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnlockedAtRequiredLevel()
    {
        var entry = new StockEntry("shelf", "Shelf", 10, 3);

        AssertThat(entry.IsLocked(3)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnlockedAboveRequiredLevel()
    {
        var entry = new StockEntry("shelf", "Shelf", 10, 3);

        AssertThat(entry.IsLocked(5)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoUnlockRowIsAlwaysLocked()
    {
        var entry = new StockEntry("shelf", "Shelf", 10, null);

        AssertThat(entry.IsLocked(99)).IsTrue();
    }
}
