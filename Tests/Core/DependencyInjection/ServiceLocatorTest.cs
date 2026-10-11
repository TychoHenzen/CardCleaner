using System;
using System.Threading.Tasks;
using CardCleaner.Scripts.Composition;
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
    public static void AutoloadRootRegistersServiceProvidersOnReinitialize()
    {
        // Arrange - a provider in the service_providers group, in the tree the way a scene provider is
        var provider = Assertions.AddNode(new TestServiceProvider());
        provider.AddToGroup("service_providers");
        var created = Array.Empty<Node>();
        try
        {
            // Act - the autoload composes synchronously, which calls RegisterServices on every provider in the group
            CompositionRoot.ReinitializeServices();
            created = ReinitializedNodes();

            // Assert
            Assertions.AssertBool(provider.RegisterServicesCalled).IsTrue();
            Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsTrue();
        }
        finally
        {
            QueueFreeAll(created);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task SecondRootLeavesContainerAndSinkUnchanged()
    {
        // Arrange - the autoload has composed, so the container, the ILog sink and the services are all set
        CompositionRoot.ReinitializeServices();
        var created = ReinitializedNodes();
        var container = ServiceLocator.Container;
        var sink = ILog.Sink;
        var log = ServiceLocator.Get<ILog>();
        var input = ServiceLocator.Get<IInputService>();
        var second = Assertions.AddNode(new CompositionRoot(), autoFree: false);
        try
        {
            // Act - the second root enters the tree; it logs an error and must leave every registration alone
            await second.ToSignal(second.GetTree(), SceneTree.SignalName.ProcessFrame);

            // Assert
            Assertions.AssertThat(ServiceLocator.Container).IsSame(container);
            Assertions.AssertThat(ILog.Sink).IsSame(sink);
            Assertions.AssertThat(ServiceLocator.Get<ILog>()).IsSame(log);
            Assertions.AssertThat(ServiceLocator.Get<IInputService>()).IsSame(input);
        }
        finally
        {
            // The second root frees whatever it composed; the nodes the autoload created are freed by the test
            second.GetParent()?.RemoveChild(second);
            second.Free();
            QueueFreeAll(created);
        }
    }

    // Reinitialize adds an input service and a player reset service to the autoload; the tests free them.
    private static Node[] ReinitializedNodes() =>
        new[] { (Node)ServiceLocator.Get<IInputService>(), (Node)ServiceLocator.Get<IPlayerResetService>() };

    private static void QueueFreeAll(Node[] nodes)
    {
        foreach (var node in nodes)
            node.QueueFree();
    }
}
