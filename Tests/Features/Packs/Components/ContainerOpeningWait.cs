using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Tests.TestUtilities.Helpers;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     Waits for a booster's opening in the hand, which plays out over real seconds
///     (<see cref="CardContainer.OpenInHand" />). The bound is wall-clock time, because headless frames are not
///     throttled and a frame count says nothing about how far an opening got. It is a timeout, not a speed check: an
///     opening takes under two seconds (<see cref="BoosterRevealPlan" />).
/// </summary>
public static class ContainerOpeningWait
{
    public const double BoundSeconds = 15;

    /// <summary>Waits until <paramref name="done" /> holds and fails the test if it still does not after the bound.</summary>
    public static async Task Until(Func<bool> done, string what)
    {
        await BoundedWait.Expect(done, what, BoundSeconds);
        // One more frame, so a wrapper queued for deletion is gone and the items it let go have been processed.
        await Frames(1);
    }

    /// <summary>Waits <paramref name="seconds" /> of wall-clock time, a frame at a time.</summary>
    public static async Task For(double seconds)
    {
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
            await Frames(1);
    }

    public static Task Frames(int count) => BoundedWait.ProcessFrames(count);
}
