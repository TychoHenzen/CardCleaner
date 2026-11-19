using System;
using System.Linq;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class ArrivalScheduleTest
{
    private const ulong Seed = 399UL;
    private const int OpenMinute = 600;
    private const int CloseMinute = 1080;

    [TestCase]
    public static void SameSeedGivesTheSameSchedule()
    {
        var first = ArrivalSchedule.Build(OpenMinute, CloseMinute, 20, new Pcg32Random(Seed));
        var second = ArrivalSchedule.Build(OpenMinute, CloseMinute, 20, new Pcg32Random(Seed));

        AssertThat(first.SequenceEqual(second)).IsTrue();
    }

    [TestCase]
    public static void ScheduleIsSortedAscending()
    {
        var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, 50, new Pcg32Random(Seed));

        AssertThat(schedule.SequenceEqual(schedule.OrderBy(minute => minute))).IsTrue();
    }

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(40)]
    public static void CountIsTheRequestedCount(int count)
    {
        var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, count, new Pcg32Random(Seed));

        AssertThat(schedule.Count).IsEqual(count);
    }

    [TestCase]
    public static void ZeroCountGivesAnEmptySchedule()
    {
        var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, 0, new Pcg32Random(Seed));

        AssertThat(schedule.Count).IsEqual(0);
    }

    [TestCase]
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

    [TestCase(6)]
    [TestCase(13)]
    public static void EachCustomerArrivesInItsOwnSliceOfTheDay(int count)
    {
        var slice = (float)(CloseMinute - ArrivalSchedule.LastEntryMarginMinutes - OpenMinute) / count;

        for (var seed = 0UL; seed < 20UL; seed++)
        {
            var schedule = ArrivalSchedule.Build(OpenMinute, CloseMinute, count, new Pcg32Random(seed));
            for (var i = 0; i < count; i++)
            {
                AssertThat(schedule[i] >= OpenMinute + (int)(slice * i)).IsTrue();
                AssertThat(schedule[i] < OpenMinute + slice * (i + 1)).IsTrue();
            }
        }
    }

    [TestCase(1080, 600, 5)]
    [TestCase(600, 600, 5)]
    [TestCase(600, 630, 5)]
    [TestCase(600, 620, 5)]
    [TestCase(600, 1080, -1)]
    public static void InvalidWindowsAndCountsAreRejected(int open, int close, int count)
    {
        AssertThrown(() => ArrivalSchedule.Build(open, close, count, new Pcg32Random(Seed)))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }
}
