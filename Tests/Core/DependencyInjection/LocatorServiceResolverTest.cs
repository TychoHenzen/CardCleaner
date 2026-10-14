using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Tests.Mocking;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Core.DependencyInjection;

/// <summary>
///     The injected resolver reads the current container at call time and shares ServiceLocator's pending-callback
///     queue, so a lookup made before its service exists recovers once the service is registered (#273, AC-2).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LocatorServiceResolverTest
{
    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DeferredGetRunsOnceAfterTheServiceIsRegistered()
    {
        var runs = 0;
        ITestService? received = null;
        TestServices.Resolver.Get<ITestService>(service =>
        {
            runs++;
            received = service;
        });
        Assertions.AssertInt(runs).IsEqual(0);

        var registered = new TestServiceImpl();
        TestServices.Container.RegisterSingleton<ITestService>(registered);
        ServiceLocator.ExecutePendingCallbacks();
        Assertions.AssertInt(runs).IsEqual(1);
        Assertions.AssertThat(received).IsSame(registered);

        ServiceLocator.ExecutePendingCallbacks();
        Assertions.AssertInt(runs).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ResolverReadsTheContainerThatReplacedTheOldOne()
    {
        var resolver = TestServices.Resolver;
        TestServices.Container.RegisterSingleton<ITestService>(new TestServiceImpl());
        Assertions.AssertBool(resolver.Has<ITestService>()).IsTrue();

        TestServices.Reset();
        Assertions.AssertBool(resolver.Has<ITestService>()).IsFalse();

        var replacement = new TestServiceImpl();
        TestServices.Container.RegisterSingleton<ITestService>(replacement);
        Assertions.AssertThat(resolver.Get<ITestService>()).IsSame(replacement);
    }
}
