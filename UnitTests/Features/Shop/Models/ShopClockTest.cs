using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class ShopClockTest
{
    [TestCase]
    public static void AdvanceWhileClosedDoesNothing()
    {
        var clock = new ShopClock();

        var reached = clock.Advance(100);

        AssertThat(reached).IsFalse();
        AssertThat(clock.SecondsIntoDay).IsEqual(0f);
    }

    [TestCase]
    public static void AdvanceWhileOpenMovesTime()
    {
        var clock = new ShopClock();
        clock.Open();

        clock.Advance(60);

        AssertThat(clock.MinutesOfDay > 480f).IsTrue();
    }

    [TestCase]
    public static void ReachingDayLengthSignalsClosing()
    {
        var clock = new ShopClock();
        clock.Open();

        AssertThat(clock.Advance(600)).IsTrue();
        AssertThat(clock.Advance(1)).IsFalse();
    }

    [TestCase]
    public static void CloseTwiceReportsTransitionOnce()
    {
        var clock = new ShopClock();
        clock.Open();

        AssertThat(clock.Close()).IsTrue();
        AssertThat(clock.Close()).IsFalse();
    }

    [TestCase]
    public static void StartNextDayResets()
    {
        var clock = new ShopClock();
        clock.Open();
        clock.Advance(100);
        clock.Close();

        clock.StartNextDay();

        AssertThat(clock.Day).IsEqual(2);
        AssertThat(clock.SecondsIntoDay).IsEqual(0f);
        AssertThat(clock.IsOpen).IsFalse();
    }

    [TestCase]
    public static void RestoreClampsDayToOne()
    {
        var clock = new ShopClock();

        clock.Restore(0);

        AssertThat(clock.Day).IsEqual(1);
    }
}
