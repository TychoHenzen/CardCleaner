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
///         gdUnit4.api awaits Godot's process_frame through a SignalAwaiter on a thread-pool thread. The awaiter is not
///         thread-safe, so a suite can never start while the main loop runs (gdUnit4Net issue 397). The package keeps
///         that code internal, so an attempt that stays silent is cancelled and the suite started again.
///     </para>
///     <para>
///         The last attempt is awaited without a bound, so the runner's stall watchdog still reports a suite that
///         never starts. CLAUDE.md names this patch and SuiteStartGuardTest keeps it in place.
///     </para>
/// </summary>
public static class SuiteStartWatchdog
{
    /// <summary>Bounds of a run: silence before a start is lost, attempts in all, wait for a cancelled run.</summary>
    public sealed record Policy(TimeSpan StartBound, int MaxAttempts, TimeSpan SettleBound);

    /// <summary>Where a run reports: the started attempt's events, lost starts, dropped events, unsettled runs.</summary>
    public sealed record Callbacks<TEvent>(
        Action<TEvent> Forward,
        Action<int> ReportLostStart,
        Action<TEvent> ReportDroppedEvent,
        Action<int> ReportUnsettled);

    /// <summary>The policy the addon runs with: three attempts, a 30 s start bound, a 5 s settle bound.</summary>
    public static readonly Policy DefaultPolicy = new(TimeSpan.FromSeconds(30), 3, TimeSpan.FromSeconds(5));

    /// <summary>Runs a suite through <paramref name="startAttempt" />, starting it again while its start is lost.</summary>
    /// <param name="startAttempt">Starts one attempt, given the receiver for its events and its token.</param>
    /// <param name="callbacks">Where the events and the reports go.</param>
    /// <param name="policy">The bounds of the run; the last attempt is never abandoned.</param>
    /// <param name="cancellation">Cancels the whole run.</param>
    /// <returns>A task that completes when the attempt that started has finished.</returns>
    public static async Task Run<TEvent>(
        Func<Action<TEvent>, CancellationToken, Task> startAttempt,
        Callbacks<TEvent> callbacks,
        Policy policy,
        CancellationToken cancellation)
    {
        for (var attempt = 1; attempt < policy.MaxAttempts; attempt++)
        {
            using var attemptCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var gate = new AttemptGate<TEvent>(callbacks.Forward, callbacks.ReportDroppedEvent);
            var run = startAttempt(gate.Receive, attemptCancellation.Token);
            if (!await StartWasLost(run, gate, policy.StartBound, cancellation))
            {
                await run;
                return;
            }

            callbacks.ReportLostStart(attempt);
            attemptCancellation.Cancel();
            if (!await Settle(run, policy.SettleBound))
                callbacks.ReportUnsettled(attempt);
        }

        var last = new AttemptGate<TEvent>(callbacks.Forward, callbacks.ReportDroppedEvent);
        await startAttempt(last.Receive, cancellation);
    }

    // ASSUMPTION (Medium): an attempt that sends no event within the bound while the main loop runs lost its start.
    // gdUnit sends a suite's first event after the suite's [Before] hook, so this holds only while no [Before] hook
    // awaits; SuiteStartGuardTest fails when one does.
    private static async Task<bool> StartWasLost<TEvent>(
        Task run, AttemptGate<TEvent> gate, TimeSpan startBound, CancellationToken cancellation)
    {
        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var delay = Task.Delay(startBound, delayCancellation.Token);
        var first = await Task.WhenAny(run, gate.Started, delay);
        delayCancellation.Cancel();
        return first == delay && !cancellation.IsCancellationRequested && gate.TryAbandon();
    }

    // An abandoned run ends through its own cancellation handling. A run that ignores its token is left behind after
    // the bound, so the next attempt still starts; its late failure is observed here and never escapes.
    private static async Task<bool> Settle(Task run, TimeSpan settleBound)
    {
        _ = run.ContinueWith(
            faulted => _ = faulted.Exception, TaskContinuationOptions.OnlyOnFaulted);
        var finished = await Task.WhenAny(run, Task.Delay(settleBound));
        return finished == run;
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
