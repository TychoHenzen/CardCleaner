using System.Threading.Tasks;

namespace CardCleaner.Tests.TestUtilities.Helpers;

[TestSuite]
[RequireGodotRuntime]
public class BoundedWaitTest
{
    [TestCase]
    public static async Task ANeverTrueConditionReturnsAMessageNamingTheWait()
    {
        var message = await BoundedWait.Until(() => false, "the shelf to settle", 0.2);

        AssertThat(message).IsNotNull();
        AssertThat(message!).Contains("the shelf to settle");
        AssertThat(message!).Contains("Timed out after");
    }

    [TestCase]
    public static async Task ATrueConditionReturnsNull()
    {
        AssertThat(await BoundedWait.Until(() => true, "anything", 0.2)).IsNull();
    }

    [TestCase]
    public static async Task ExpectReturnsOnceTheConditionHolds()
    {
        var checks = 0;

        await BoundedWait.Expect(() => ++checks >= 3, "the third check", 1);

        AssertThat(checks).IsEqual(3);
    }

    [TestCase]
    public static async Task ProcessFramesWaitsAtLeastThatManyProcessFrames()
    {
        var first = Godot.Engine.GetProcessFrames();

        await BoundedWait.ProcessFrames(3);

        AssertThat(Godot.Engine.GetProcessFrames() - first).IsGreaterEqual(3ul);
    }

    [TestCase]
    public static async Task SyncPhysicsFramesWaitsAtLeastThatManyPhysicsFrames()
    {
        var first = Godot.Engine.GetPhysicsFrames();

        await BoundedWait.SyncPhysicsFrames(3);

        AssertThat(Godot.Engine.GetPhysicsFrames() - first).IsGreaterEqual(3ul);
    }

    [TestCase]
    public static async Task ProcessFramesUntilReturnsTrueAtOnceWhenTheConditionAlreadyHolds()
    {
        var first = Godot.Engine.GetProcessFrames();

        AssertThat(await BoundedWait.ProcessFramesUntil(() => true, 5)).IsTrue();

        AssertThat(Godot.Engine.GetProcessFrames() - first).IsEqual(0ul);
    }

    [TestCase]
    public static async Task ProcessFramesUntilReturnsTrueOnceTheConditionHolds()
    {
        var first = Godot.Engine.GetProcessFrames();

        var held = await BoundedWait.ProcessFramesUntil(() => Godot.Engine.GetProcessFrames() - first >= 2, 30);

        AssertThat(held).IsTrue();
        AssertThat(Godot.Engine.GetProcessFrames() - first).IsGreaterEqual(2ul);
    }

    [TestCase]
    public static async Task ProcessFramesUntilStopsAfterTheFrameBudgetAndReturnsFalse()
    {
        var first = Godot.Engine.GetProcessFrames();

        var held = await BoundedWait.ProcessFramesUntil(() => false, 3);

        AssertThat(held).IsFalse();
        AssertThat(Godot.Engine.GetProcessFrames() - first).IsGreaterEqual(3ul);
    }

    [TestCase]
    public static async Task SyncPhysicsFramesUntilReturnsTrueOnceTheConditionHolds()
    {
        var first = Godot.Engine.GetPhysicsFrames();

        var held = await BoundedWait.SyncPhysicsFramesUntil(() => Godot.Engine.GetPhysicsFrames() - first >= 2, 30);

        AssertThat(held).IsTrue();
        AssertThat(Godot.Engine.GetPhysicsFrames() - first).IsGreaterEqual(2ul);
    }

    [TestCase]
    public static async Task SyncPhysicsFramesUntilStopsAfterTheFrameBudgetAndReturnsFalse()
    {
        var first = Godot.Engine.GetPhysicsFrames();

        var held = await BoundedWait.SyncPhysicsFramesUntil(() => false, 3);

        AssertThat(held).IsFalse();
        AssertThat(Godot.Engine.GetPhysicsFrames() - first).IsGreaterEqual(3ul);
    }

    [TestCase]
    public static async Task TheNextCaseStillRunsAfterABoundedWaitFailed()
    {
        await BoundedWait.Until(() => false, "never", 0.1);

        AssertThat(await BoundedWait.PhysicsFrames(3)).IsNull();
    }
}
