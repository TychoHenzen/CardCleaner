using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Tests.Mocking;

namespace CardCleaner.Tests.Core.DependencyInjection;

[TestSuite]
[RequireGodotRuntime]
public class ServiceLocatorTest
{
    [AfterTest]
    public static void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestGetRegisteredService()
    {
        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);

        var retrieved = ServiceLocator.Get<IAsyncTestService>();

        Assertions.AssertThat(retrieved).IsEqual(testService);
        Assertions.AssertThat(retrieved.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestHasService()
    {
        Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsFalse();

        ServiceLocator.Container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();

        Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestGetUnregisteredServiceThrows()
    {
        Assertions.AssertThrown(() => ServiceLocator.Get<IAsyncTestService>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("Service IAsyncTestService not registered");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestAsyncServiceCallback()
    {
        IAsyncTestService? callbackService = null;
        var callbackExecuted = false;

        // Register callback before service is available
        ServiceLocator.Get<IAsyncTestService>(service =>
        {
            callbackService = service;
            callbackExecuted = true;
        });

        Assertions.AssertBool(callbackExecuted).IsFalse();

        ServiceLocator.Container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();
        ServiceLocator.ExecutePendingCallbacks();

        Assertions.AssertBool(callbackExecuted).IsTrue();
        Assertions.AssertThat(callbackService).IsNotNull();
        Assertions.AssertThat(callbackService?.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestImmediateCallbackForRegisteredService()
    {
        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);

        IAsyncTestService? callbackService = null;
        var callbackExecuted = false;

        // Callback should execute immediately since service is already registered
        ServiceLocator.Get<IAsyncTestService>(service =>
        {
            callbackService = service;
            callbackExecuted = true;
        });

        Assertions.AssertBool(callbackExecuted).IsTrue();
        Assertions.AssertThat(callbackService).IsEqual(testService);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestMultipleCallbacksForSameService()
    {
        var callback1Executed = false;
        var callback2Executed = false;
        IAsyncTestService? service1 = null;
        IAsyncTestService? service2 = null;

        // Register multiple callbacks
        ServiceLocator.Get<IAsyncTestService>(service =>
        {
            service1 = service;
            callback1Executed = true;
        });

        ServiceLocator.Get<IAsyncTestService>(service =>
        {
            service2 = service;
            callback2Executed = true;
        });

        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);
        ServiceLocator.ExecutePendingCallbacks();

        // Both callbacks should execute
        Assertions.AssertBool(callback1Executed).IsTrue();
        Assertions.AssertBool(callback2Executed).IsTrue();
        Assertions.AssertThat(service1).IsEqual(testService);
        Assertions.AssertThat(service2).IsEqual(testService);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestCallbackNotExecutedIfServiceNotRegistered()
    {
        var callbackExecuted = false;

        ServiceLocator.Get<IAsyncTestService>(_ => { callbackExecuted = true; });

        // Execute callbacks without registering service
        ServiceLocator.ExecutePendingCallbacks();

        Assertions.AssertBool(callbackExecuted).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestContainerAccessibility()
    {
        var container = ServiceLocator.Container;

        Assertions.AssertThat(container).IsNotNull();
        Assertions.AssertThat(container).IsInstanceOf<IServiceContainer>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestServiceRegistrationViaContainer()
    {
        // Test that we can register services directly via the container
        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);

        Assertions.AssertBool(ServiceLocator.Container.IsRegistered<IAsyncTestService>()).IsTrue();

        var retrieved = ServiceLocator.Container.Resolve<IAsyncTestService>();
        Assertions.AssertThat(retrieved).IsEqual(testService);
    }
}
