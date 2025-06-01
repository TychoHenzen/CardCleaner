using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using GdUnit4;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Tests.Core;

// Test service interfaces

[TestSuite]
public class ServiceLocatorTest
{
    private ServiceLocator _locator;
    private SceneTree _testTree;

    [Before]
    public void Setup()
    {
        // Reset static state before each test
        ServiceLocator.ResetForTesting();
    
        // Create a minimal scene tree for testing
        _testTree = new SceneTree();
    
        _locator = new ServiceLocator();
        _locator.Name = "Services";
    
        // Simulate the autoload setup
        _testTree.Root.AddChild(_locator);
    }


    [After]
    public void Cleanup()
    {
        _locator?.QueueFree();
        _testTree?.Quit();
    }

    [TestCase]
    public void TestGetRegisteredService()
    {
        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);

        var retrieved = ServiceLocator.Get<IAsyncTestService>();
        
        Assertions.AssertThat(retrieved).IsEqual(testService);
        Assertions.AssertThat(retrieved.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    public void TestHasService()
    {
        Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsFalse();
        
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();
        
        Assertions.AssertBool(ServiceLocator.Has<IAsyncTestService>()).IsTrue();
    }

    [TestCase]
    public void TestGetUnregisteredServiceThrows()
    {
        Assertions.AssertThrown(() => ServiceLocator.Get<IAsyncTestService>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("Service IAsyncTestService not registered");
    }

    [TestCase]
    public void TestAsyncServiceCallback()
    {
        IAsyncTestService callbackService = null;
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
        Assertions.AssertThat(callbackService.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    public void TestImmediateCallbackForRegisteredService()
    {
        var testService = new AsyncTestServiceImpl();
        ServiceLocator.Container.RegisterSingleton<IAsyncTestService>(testService);
        
        IAsyncTestService callbackService = null;
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
    public void TestMultipleCallbacksForSameService()
    {
        var callback1Executed = false;
        var callback2Executed = false;
        IAsyncTestService service1 = null;
        IAsyncTestService service2 = null;
        
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
    public void TestCallbackNotExecutedIfServiceNotRegistered()
    {
        var callbackExecuted = false;
        
        ServiceLocator.Get<IAsyncTestService>(service => 
        {
            callbackExecuted = true;
        });
        
        // Execute callbacks without registering service
        ServiceLocator.ExecutePendingCallbacks();
        
        Assertions.AssertBool(callbackExecuted).IsFalse();
    }

    [TestCase]
    public void TestContainerAccessibility()
    {
        var container = ServiceLocator.Container;
        
        Assertions.AssertThat(container).IsNotNull();
        Assertions.AssertThat(container).IsInstanceOf<IServiceContainer>();
    }

    [TestCase]
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
    public void TestServiceProviderGroupHandling()
    {
        // Create a mock service provider
        var mockProvider = new MockServiceProvider();
        mockProvider.AddToGroup("service_providers");
        _testTree.Root.AddChild(mockProvider);
        
        // Simulate the service provider registration process
        // Note: In real usage, this happens in _Ready() via CallDeferred
        
        Assertions.AssertThat(mockProvider.RegisterServicesCalled).IsFalse();
        
        // Clean up
        mockProvider.QueueFree();
    }
}

// Mock service provider for testing
public partial class MockServiceProvider : Node, IServiceProvider
{
    public bool RegisterServicesCalled { get; private set; }
    
    public void RegisterServices(IServiceContainer container)
    {
        RegisterServicesCalled = true;
        // Register a test service
        container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();
    }
}