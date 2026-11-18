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
    public static async Task TheNextCaseStillRunsAfterABoundedWaitFailed()
    {
        await BoundedWait.Until(() => false, "never", 0.1);

        AssertThat(await BoundedWait.PhysicsFrames(3)).IsNull();
    }
}
