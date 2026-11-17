using System;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CardCleaner.Tests.Core.Resources;

/// <summary>
///     Holds the .NET finalizer thread inside one finalizer until <see cref="Open" /> is called or a bound passes, so
///     a test can act between a collection and the finalizers that collection queued.
/// </summary>
internal static class FinalizerGate
{
    private static readonly TimeSpan ParkBound = TimeSpan.FromSeconds(5);
    private static readonly ManualResetEventSlim Parked = new();
    private static readonly ManualResetEventSlim Opened = new();

    /// <summary>Parks the finalizer thread, which goes on by itself after <paramref name="hold" /> at most.</summary>
    public static void Park(TimeSpan hold)
    {
        Parked.Reset();
        Opened.Reset();
        Abandon(hold);
        GC.Collect();
        if (!Parked.Wait(ParkBound))
            throw new InvalidOperationException("The finalizer thread did not reach the gate.");
    }

    /// <summary>Lets the parked finalizer thread go on.</summary>
    public static void Open() => Opened.Set();

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Abandon(TimeSpan hold) => _ = new Blocker(hold);

    private sealed class Blocker(TimeSpan hold)
    {
        ~Blocker()
        {
            Parked.Set();
            Opened.Wait(hold);
        }
    }
}
