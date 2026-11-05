using System.Threading.Tasks;

namespace CardCleaner.Tests.TestUtilities.Helpers;

[TestSuite]
[RequireGodotRuntime]
public class BoundedWaitTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static async Task ANeverTrueConditionReturnsAMessageNamingTheWait()
    {
        var message = await BoundedWait.Until(() => false, "the shelf to settle", 0.2);

        AssertThat(message).IsNotNull();
        AssertThat(message!).Contains("the shelf to settle");
        AssertThat(message!).Contains("Timed out after");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task ATrueConditionReturnsNull()
    {
        AssertThat(await BoundedWait.Until(() => true, "anything", 0.2)).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task TheNextCaseStillRunsAfterABoundedWaitFailed()
    {
        await BoundedWait.Until(() => false, "never", 0.1);

        AssertThat(await BoundedWait.PhysicsFrames(3)).IsNull();
    }
}
