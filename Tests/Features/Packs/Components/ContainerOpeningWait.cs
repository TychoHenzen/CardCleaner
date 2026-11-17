using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Tests.TestUtilities.Helpers;
using Godot;

namespace CardCleaner.Tests.Features.Packs.Components;

/// <summary>
///     Waits for a container's opening, which plays out over real seconds (<see cref="CardContainer.Open" />). The
///     bound is wall-clock time, because headless frames are not throttled and a frame count says nothing about how
///     far an opening got. It is a timeout, not a speed check: the longest opening takes under three seconds.
/// </summary>
public static class ContainerOpeningWait
{
    public const double BoundSeconds = 15;

    /// <summary>Waits until <paramref name="container" /> has finished opening and been freed.</summary>
    public static Task UntilOpened(CardContainer container) =>
        Until(() => !GodotObject.IsInstanceValid(container) || container.IsQueuedForDeletion(),
            $"opening the {container.Kind}");

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
