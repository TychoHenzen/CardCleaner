using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class RestockerJobTest
{
    private static readonly RestockStep Step = new(RestockStepKind.Move, 4, 9);

    [TestCase]
    [TestCategory("Unit")]
    public static void Begin_then_Arrived_twice_ends_Idle_and_the_second_returns_true()
    {
        var job = new RestockerJob();
        job.Begin(Step);
        AssertThat(job.Phase).IsEqual(RestockerPhase.ToSource);

        AssertBool(job.Arrived()).IsFalse();
        AssertThat(job.Phase).IsEqual(RestockerPhase.ToShelf);
        AssertBool(job.Arrived()).IsTrue();
        AssertThat(job.Phase).IsEqual(RestockerPhase.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TargetLost_returns_Idle_and_blocks_the_pair()
    {
        var job = new RestockerJob();
        job.Begin(Step);
        job.Arrived();

        job.TargetLost();

        AssertThat(job.Phase).IsEqual(RestockerPhase.Idle);
        AssertBool(job.BlockedPairs.Contains((4, 9))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Pair_unblocks_after_BlockSeconds_and_not_before()
    {
        var job = new RestockerJob();
        job.Begin(Step);
        job.TargetLost();

        job.Tick(RestockerJob.BlockSeconds - 1);
        AssertBool(job.BlockedPairs.Contains((4, 9))).IsTrue();

        job.Tick(1);
        AssertBool(job.BlockedPairs.Contains((4, 9))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NoPath_behaves_like_TargetLost()
    {
        var job = new RestockerJob();
        job.Begin(Step);

        job.NoPath();

        AssertThat(job.Phase).IsEqual(RestockerPhase.Idle);
        AssertBool(job.BlockedPairs.Contains((4, 9))).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Cancel_returns_Idle_without_blocking_the_pair()
    {
        var job = new RestockerJob();
        job.Begin(Step);
        job.Arrived();

        job.Cancel();

        AssertThat(job.Phase).IsEqual(RestockerPhase.Idle);
        AssertThat(job.Current).IsEqual(RestockStep.Idle);
        AssertThat(job.BlockedPairs.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Calls_in_Idle_change_nothing()
    {
        var job = new RestockerJob();

        AssertBool(job.Arrived()).IsFalse();
        job.TargetLost();
        job.NoPath();
        job.Begin(RestockStep.Idle);

        AssertThat(job.Phase).IsEqual(RestockerPhase.Idle);
        AssertThat(job.BlockedPairs.Count).IsEqual(0);
        AssertThat(job.Current).IsEqual(RestockStep.Idle);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void Begin_while_busy_is_ignored()
    {
        var job = new RestockerJob();
        job.Begin(Step);

        job.Begin(new RestockStep(RestockStepKind.Move, 1, 2));

        AssertThat(job.Current).IsEqual(Step);
    }
}
