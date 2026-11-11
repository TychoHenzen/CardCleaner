using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using gdUnit4.addons.gdUnit4.src.dotnet;

namespace CardCleaner.Tests.Addons.TestRunner;

/// <summary>
/// SuiteStartWatchdog (issue 448) starts a C# suite again when gdUnit loses its start. Each case drives it with a fake
/// bridge whose attempts either send an event, stay silent until cancelled (a lost start), or send late.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SuiteStartWatchdogTest
{
    private static readonly TimeSpan Bound = TimeSpan.FromMilliseconds(100);
    private const int Attempts = 3;

    [TestCase]
    [TestCategory("Unit")]
    public static async Task AnAttemptThatStartsRunsOnce()
    {
        var bridge = new FakeBridge(Starts);

        await bridge.Run(Attempts);

        AssertThat(bridge.AttemptsStarted).IsEqual(1);
        AssertThat(bridge.LostStarts).IsEmpty();
        AssertThat(bridge.Forwarded).ContainsExactly("event 1");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ASilentAttemptIsAbandonedAndTheSuiteStartsAgain()
    {
        var bridge = new FakeBridge(StaysSilent, Starts);

        await bridge.Run(Attempts);

        AssertThat(bridge.AttemptsStarted).IsEqual(2);
        AssertThat(bridge.LostStarts).ContainsExactly(1);
        AssertThat(bridge.CancelledAttempts).ContainsExactly(1);
        AssertThat(bridge.Forwarded).ContainsExactly("event 2");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task AnEventFromAnAbandonedAttemptIsDropped()
    {
        var bridge = new FakeBridge(StaysSilent, Starts);
        await bridge.Run(Attempts);

        bridge.Receivers[0]("late event 1");

        AssertThat(bridge.Forwarded).ContainsExactly("event 2");
        AssertThat(bridge.Dropped).ContainsExactly("late event 1");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task TheLastAttemptIsNeverAbandoned()
    {
        var bridge = new FakeBridge(StaysSilent, StartsLate);

        await bridge.Run(maxAttempts: 2);

        AssertThat(bridge.AttemptsStarted).IsEqual(2);
        AssertThat(bridge.LostStarts).ContainsExactly(1);
        AssertThat(bridge.Forwarded).ContainsExactly("event 2");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ACancelledRunIsNotStartedAgain()
    {
        var bridge = new FakeBridge(StaysSilent, Starts);
        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

        await bridge.Run(Attempts, cancellation.Token);

        AssertThat(bridge.AttemptsStarted).IsEqual(1);
        AssertThat(bridge.LostStarts).IsEmpty();
        AssertThat(bridge.Forwarded).IsEmpty();
    }

    // The attempt sends its event at once and finishes, as a suite that started does.
    private static async Task Starts(int attempt, Action<string> receive, CancellationToken token)
    {
        await Task.Yield();
        receive($"event {attempt}");
    }

    // The attempt sends its event only after three start bounds, then finishes.
    private static async Task StartsLate(int attempt, Action<string> receive, CancellationToken token)
    {
        await Task.Delay(Bound * 3, CancellationToken.None);
        receive($"event {attempt}");
    }

    // The attempt sends nothing until it is cancelled, as a suite whose start was lost; like the real bridge, it
    // ends quietly on cancellation.
    private static async Task StaysSilent(int attempt, Action<string> receive, CancellationToken token)
    {
        try
        {
            await Task.Delay(Timeout.Infinite, token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private delegate Task Attempt(int attempt, Action<string> receive, CancellationToken token);

    private sealed class FakeBridge(params Attempt[] attempts)
    {
        public int AttemptsStarted { get; private set; }
        public List<Action<string>> Receivers { get; } = new();
        public List<int> CancelledAttempts { get; } = new();
        public List<int> LostStarts { get; } = new();
        public List<string> Forwarded { get; } = new();
        public List<string> Dropped { get; } = new();

        public Task Run(int maxAttempts, CancellationToken cancellation = default) =>
            SuiteStartWatchdog.Run<string>(
                Start, Forwarded.Add, LostStarts.Add, Dropped.Add, Bound, maxAttempts, cancellation);

        private Task Start(Action<string> receive, CancellationToken token)
        {
            var attempt = ++AttemptsStarted;
            Receivers.Add(receive);
            token.Register(() => CancelledAttempts.Add(attempt));
            return attempts[attempt - 1](attempt, receive, token);
        }
    }
}
