// Project patch to the vendored gdUnit4 addon (issue 448). Keep it, and its call in GdUnit4CSharpApi.ExecuteAsync,
// when the addon is updated; CLAUDE.md names it and SuiteStartGuardTest fails without it.
#pragma warning disable IDE1006
namespace gdUnit4.addons.gdUnit4.src.dotnet;
#pragma warning restore IDE1006

using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
///     Starts one C# test suite again when its start is lost.
///     <para>
///         gdUnit4.api runs a suite on a thread-pool thread, and the first thing the suite does there is await Godot's
///         process_frame through a SignalAwaiter. That awaiter is not thread-safe: when the main thread emits the
///         signal between the awaiter's completion check and its continuation registration, the continuation is never
///         run and the suite never starts, while the main loop keeps running (gdUnit4Net issue 397). The package keeps
///         that code internal, so the project cannot run the suite on the main thread instead.
///     </para>
///     <para>
///         An attempt that sends no event within the start bound is abandoned: its run is cancelled, every later event
///         from it is dropped, and the suite is started again, at most <c>maxAttempts</c> times in all. The last
///         attempt is awaited without a bound, so the runner's stall watchdog still reports a suite that never starts.
///     </para>
/// </summary>
public static class SuiteStartWatchdog
{
    /// <summary>Attempts per suite, the first one included.</summary>
    public const int DefaultMaxAttempts = 3;

    /// <summary>How long an attempt may stay silent before its start counts as lost.</summary>
    public static readonly TimeSpan DefaultStartBound = TimeSpan.FromSeconds(30);

    /// <summary>
    ///     Runs a suite through <paramref name="startAttempt" /> and starts it again while its start is lost.
    /// </summary>
    /// <param name="startAttempt">Starts one attempt: it gets the receiver for the attempt's events and its token.</param>
    /// <param name="forward">Receives the events of the attempt that started.</param>
    /// <param name="reportLostStart">Called with the attempt number of each abandoned attempt.</param>
    /// <param name="reportDroppedEvent">Called for each event an abandoned attempt sends after all.</param>
    /// <param name="startBound">How long an attempt may stay silent before it is abandoned.</param>
    /// <param name="maxAttempts">Attempts in all; the last one is never abandoned.</param>
    /// <param name="cancellation">Cancels the whole run.</param>
    /// <returns>A task that completes when the attempt that started has finished.</returns>
    public static async Task Run<TEvent>(
        Func<Action<TEvent>, CancellationToken, Task> startAttempt,
        Action<TEvent> forward,
        Action<int> reportLostStart,
        Action<TEvent> reportDroppedEvent,
        TimeSpan startBound,
        int maxAttempts,
        CancellationToken cancellation)
    {
        for (var attempt = 1; ; attempt++)
        {
            using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var gate = new AttemptGate<TEvent>(forward, reportDroppedEvent);
            var run = startAttempt(gate.Receive, attemptCancellation.Token);
            if (attempt < maxAttempts && await StartWasLost(run, gate, startBound, cancellation))
            {
                reportLostStart(attempt);
                attemptCancellation.Cancel();
                await Settle(run);
                continue;
            }

            await run;
            return;
        }
    }

    // ASSUMPTION (Medium): an attempt that sends no event within the bound while the main loop runs lost its start.
    // gdUnit sends a suite's first event after the suite's [Before] hook, so this holds only while no [Before] hook
    // awaits; SuiteStartGuardTest fails when one does. A synchronous hook blocks the main thread, and this check,
    // which resumes on the main thread, until the hook has returned and the first event is sent.
    private static async Task<bool> StartWasLost<TEvent>(
        Task run, AttemptGate<TEvent> gate, TimeSpan startBound, CancellationToken cancellation)
    {
        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var delay = Task.Delay(startBound, delayCancellation.Token);
        var first = await Task.WhenAny(run, gate.Started, delay);
        delayCancellation.Cancel();
        return first == delay && !cancellation.IsCancellationRequested && gate.TryAbandon();
    }

    // An abandoned run ends through its own cancellation handling; a cancellation thrown out of it is expected.
    private static async Task Settle(Task run)
    {
        try
        {
            await run;
        }
        catch (OperationCanceledException)
        {
        }
    }

    // Lets the events of one attempt through until the attempt is abandoned. Starting and abandoning take the same
    // lock, so an attempt is either started (its events go on) or abandoned (its events are dropped), never both.
    private sealed class AttemptGate<TEvent>(Action<TEvent> forward, Action<TEvent> reportDroppedEvent)
    {
        private readonly object _lock = new();
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _abandoned;

        public Task Started => _started.Task;

        public void Receive(TEvent testEvent)
        {
            bool abandoned;
            lock (_lock)
            {
                abandoned = _abandoned;
                if (!abandoned)
                    _started.TrySetResult();
            }

            if (abandoned)
                reportDroppedEvent(testEvent);
            else
                forward(testEvent);
        }

        public bool TryAbandon()
        {
            lock (_lock)
            {
                if (_started.Task.IsCompleted)
                    return false;
                _abandoned = true;
                return true;
            }
        }
    }
}
