using System;
using System.Linq;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class ArrivalScheduleTest
{
    private const ulong Seed = 399UL;
    private const int OpenMinute = 600;
    private const int CloseMinute = 1080;

    [TestCase]
    [TestCategory("Unit")]
    public static void SameSeedGivesTheSameSchedule()
    {
        var first = ArrivalSchedule.Build(OpenMinute, CloseMinute, 20, new Pcg32Random(Seed));
        var second = ArrivalSchedule.Build(OpenMinute, CloseMinute, 20, new Pcg32Random(Seed));

        AssertThat(first.SequenceEqual(second)).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScheduleIsSortedAscending()
    {
        var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, 50, new Pcg32Random(Seed));

        AssertThat(schedule.SequenceEqual(schedule.OrderBy(minute => minute))).IsTrue();
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(40)]
    [TestCategory("Unit")]
    public static void CountIsTheRequestedCount(int count)
    {
        var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, count, new Pcg32Random(Seed));

        AssertThat(schedule.Count).IsEqual(count);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ZeroCountGivesAnEmptySchedule()
    {
        var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, 0, new Pcg32Random(Seed));

        AssertThat(schedule.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryArrivalFallsBeforeTheLastEntryMargin()
    {
        var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, 200, new Pcg32Random(Seed));
        var lastAllowed = CloseMinute - ArrivalSchedule.LastEntryMarginMinutes;

        foreach (var minute in schedule)
        {
            AssertThat(minute >= OpenMinute).IsTrue();
            AssertThat(minute < lastAllowed).IsTrue();
        }
    }

    [TestCase(1080, 600, 5)]
    [TestCase(600, 600, 5)]
    [TestCase(600, 630, 5)]
    [TestCase(600, 620, 5)]
    [TestCase(600, 1080, -1)]
    [TestCategory("Unit")]
    public static void InvalidWindowsAndCountsAreRejected(int open, int close, int count)
    {
        AssertThrown(() => ArrivalSchedule.Build(open, close, count, new Pcg32Random(Seed)))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
