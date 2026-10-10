using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Tests.Mocking;
using Godot;

namespace CardCleaner.Tests.Core.DependencyInjection;

[TestSuite]
[RequireGodotRuntime]
public class ServiceLocatorTest
{
    [BeforeTest]
    public void Setup()
    {
    }


    [AfterTest]
    public void TearDown()
    {
        ServiceLocator.ResetForTesting();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestGetRegisteredService()
    {
        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);

        var retrieved = ServiceLocator.Get<IAsyncTestService>();

        Assertions.AssertThat(retrieved).IsEqual(testService);
        Assertions.AssertThat(retrieved.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestHasService()
    {
        Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsFalse();

        ServiceLocator.Container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();

        Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestGetUnregisteredServiceThrows()
    {
        Assertions.AssertInt(5).IsLess(8);
        Assertions.AssertThrown(() => ServiceLocator.Get<IAsyncTestService>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("Service IAsyncTestService not registered");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestAsyncServiceCallback()
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

        // Register the service
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();

        // Execute pending callbacks
        ServiceLocator.ExecutePendingCallbacks();

        Assertions.AssertBool(callbackExecuted).IsTrue();
        Assertions.AssertThat(callbackService).IsNotNull();
        Assertions.AssertThat(callbackService?.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestImmediateCallbackForRegisteredService()
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
    public void TestMultipleCallbacksForSameService()
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

        // Register service
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
    public void TestCallbackNotExecutedIfServiceNotRegistered()
    {
        var callbackExecuted = false;

        ServiceLocator.Get<IAsyncTestService>(_ => { callbackExecuted = true; });

        // Execute callbacks without registering service
        ServiceLocator.ExecutePendingCallbacks();

        Assertions.AssertBool(callbackExecuted).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestContainerAccessibility()
    {
        var container = ServiceLocator.Container;

        Assertions.AssertThat(container).IsNotNull();
        Assertions.AssertThat(container).IsInstanceOf<IServiceContainer>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TestServiceRegistrationViaContainer()
    {
        // Test that we can register services directly via the container
        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);

        Assertions.AssertBool(ServiceLocator.Container.IsRegistered<IAsyncTestService>()).IsTrue();

        var retrieved = ServiceLocator.Container.Resolve<IAsyncTestService>();
        Assertions.AssertThat(retrieved).IsEqual(testService);
    }

    [TestCase]
    [TestCategory("Unit")]
    public async Task TestServiceProviderGroupHandling()
    {
        // Create a test service provider that implements both Node and IServiceProvider
        var testProvider = new TestServiceProvider();
        testProvider.AddToGroup("service_providers");

        // Actually trigger the service registration process
        ServiceLocator.ResetForTesting();
        var containerBefore = ServiceLocator.Container;
        var serviceLocator = Assertions.AddNode(new ServiceLocator(), autoFree: false);
        try
        {
            // Add the provider as a child so it's in the scene tree
            serviceLocator.AddChild(testProvider);

            // Trigger the registration process (normally happens in _Ready via CallDeferred)
            serviceLocator.CallDeferred(ServiceLocator.MethodName.ResolveServices);

            // Wait for deferred call to complete
            await serviceLocator.ToSignal(serviceLocator.GetTree(), SceneTree.SignalName.ProcessFrame);

            // Verify the service was registered
            Assertions.AssertThat(testProvider.RegisterServicesCalled).IsTrue();
            Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsTrue();
        }
        finally
        {
            // The test frees its locator itself, so the instance it replaced is restored before the next test
            serviceLocator.GetParent()?.RemoveChild(serviceLocator);
            serviceLocator.Free();
        }

        Assertions.AssertThat(ServiceLocator.Container)
            .OverrideFailureMessage("freeing the locator must restore the instance current before this test")
            .IsSame(containerBefore);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestSecondLocatorLeavingTreeRestoresReplacedInstance()
    {
        // Arrange - the first locator becomes the static instance, then the second replaces it on _Ready.
        // The test frees both locators itself, so the restore does not depend on gdUnit's auto-free.
        var containerBefore = ServiceLocator.Container;
        var first = Assertions.AddNode(new ServiceLocator(), autoFree: false);
        try
        {
            var second = new ServiceLocator();
            first.AddChild(second);

            // Act - the second locator leaves the tree and is freed by the test
            try
            {
                first.RemoveChild(second);
            }
            finally
            {
                second.Free();
            }

            // Assert - the first locator is the instance again, so its services reinitialize into it
            ServiceLocator.ReinitializeServices();
            var inputService = ServiceLocator.Get<IInputService>() as Node;
            Assertions.AssertThat(inputService)
                .OverrideFailureMessage("IInputService must resolve once the second locator has left the tree")
                .IsNotNull();
            Assertions.AssertBool(GodotObject.IsInstanceValid(inputService))
                .OverrideFailureMessage("IInputService must still be a live node")
                .IsTrue();
            Assertions.AssertThat(inputService?.GetParent())
                .OverrideFailureMessage("IInputService must be parented to the first locator")
                .IsSame(first);
        }
        finally
        {
            // Leaving the tree hands the static instance back to the one that was current before this test
            first.GetParent()?.RemoveChild(first);
            first.Free();
        }

        Assertions.AssertThat(ServiceLocator.Container)
            .OverrideFailureMessage("freeing the first locator must restore the instance current before this test")
            .IsSame(containerBefore);
    }
}
