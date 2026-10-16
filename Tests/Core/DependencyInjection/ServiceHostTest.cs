using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Tests.Mocking;

namespace CardCleaner.Tests.Core.DependencyInjection;

/// <summary>
///     A <see cref="ServiceHost" /> built with <c>new</c> owns its own container and pending-callback queue and
///     touches no global state. It reads its current container at call time and shares one queue, so a lookup made
///     before its service exists recovers once the service is registered (#273, AC-2).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ServiceHostTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TestGetRegisteredService()
    {
        var host = new ServiceHost();
        var testService = new AsyncTestServiceImpl();
        host.Container.RegisterSingleton<IAsyncTestService>(testService);

        var retrieved = host.Get<IAsyncTestService>();

        Assertions.AssertThat(retrieved).IsEqual(testService);
        Assertions.AssertThat(retrieved.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestHasService()
    {
        var host = new ServiceHost();
        Assertions.AssertBool(host.Has<IAsyncTestService>()).IsFalse();

        host.Container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();

        Assertions.AssertBool(host.Has<IAsyncTestService>()).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestGetUnregisteredServiceThrows()
    {
        var host = new ServiceHost();

        Assertions.AssertThrown(() => host.Get<IAsyncTestService>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("Service IAsyncTestService not registered");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestAsyncServiceCallback()
    {
        var host = new ServiceHost();
        IAsyncTestService? callbackService = null;
        var callbackExecuted = false;

        // Register callback before service is available
        host.Get<IAsyncTestService>(service =>
        {
            callbackService = service;
            callbackExecuted = true;
        });

        Assertions.AssertBool(callbackExecuted).IsFalse();

        host.Container.RegisterSingleton<IAsyncTestService, AsyncTestServiceImpl>();
        host.ExecutePendingCallbacks();

        Assertions.AssertBool(callbackExecuted).IsTrue();
        Assertions.AssertThat(callbackService).IsNotNull();
        Assertions.AssertThat(callbackService?.GetData()).IsEqual("async_test_data");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestImmediateCallbackForRegisteredService()
    {
        var host = new ServiceHost();
        var testService = new AsyncTestServiceImpl();
        host.Container.RegisterSingleton<IAsyncTestService>(testService);

        IAsyncTestService? callbackService = null;
        var callbackExecuted = false;

        // Callback should execute immediately since service is already registered
        host.Get<IAsyncTestService>(service =>
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
        var host = new ServiceHost();
        var callback1Executed = false;
        var callback2Executed = false;
        IAsyncTestService? service1 = null;
        IAsyncTestService? service2 = null;

        // Register multiple callbacks
        host.Get<IAsyncTestService>(service =>
        {
            service1 = service;
            callback1Executed = true;
        });

        host.Get<IAsyncTestService>(service =>
        {
            service2 = service;
            callback2Executed = true;
        });

        var testService = new AsyncTestServiceImpl();
        host.Container.RegisterSingleton<IAsyncTestService>(testService);
        host.ExecutePendingCallbacks();

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
        var host = new ServiceHost();
        var callbackExecuted = false;

        host.Get<IAsyncTestService>(_ => { callbackExecuted = true; });

        // Execute callbacks without registering service
        host.ExecutePendingCallbacks();

        Assertions.AssertBool(callbackExecuted).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestContainerAccessibility()
    {
        var container = new ServiceHost().Container;

        Assertions.AssertThat(container).IsNotNull();
        Assertions.AssertThat(container).IsInstanceOf<IServiceContainer>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestServiceRegistrationViaContainer()
    {
        var host = new ServiceHost();
        // Test that we can register services directly via the container
        var testService = new AsyncTestServiceImpl();
        host.Container.RegisterSingleton<IAsyncTestService>(testService);

        Assertions.AssertBool(host.Container.IsRegistered<IAsyncTestService>()).IsTrue();

        var retrieved = host.Container.Resolve<IAsyncTestService>();
        Assertions.AssertThat(retrieved).IsEqual(testService);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void DeferredGetRunsOnceAfterTheServiceIsRegistered()
    {
        var host = new ServiceHost();
        var runs = 0;
        ITestService? received = null;
        host.Get<ITestService>(service =>
        {
            runs++;
            received = service;
        });
        Assertions.AssertInt(runs).IsEqual(0);

        var registered = new TestServiceImpl();
        host.Container.RegisterSingleton<ITestService>(registered);
        host.ExecutePendingCallbacks();
        Assertions.AssertInt(runs).IsEqual(1);
        Assertions.AssertThat(received).IsSame(registered);

        host.ExecutePendingCallbacks();
        Assertions.AssertInt(runs).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ResolverReadsTheContainerThatReplacedTheOldOne()
    {
        var host = new ServiceHost();
        IServiceResolver resolver = host;
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());
        Assertions.AssertBool(resolver.Has<ITestService>()).IsTrue();

        host.Reset();
        Assertions.AssertBool(resolver.Has<ITestService>()).IsFalse();

        var replacement = new TestServiceImpl();
        host.Container.RegisterSingleton<ITestService>(replacement);
        Assertions.AssertThat(resolver.Get<ITestService>()).IsSame(replacement);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ResetDropsTheQueuedCallbacks()
    {
        var host = new ServiceHost();
        var runs = 0;
        host.Get<ITestService>(_ => runs++);

        host.Reset();
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());
        host.ExecutePendingCallbacks();

        Assertions.AssertInt(runs).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ThrowingCallbackDoesNotRerunEarlierCallbacks()
    {
        var host = new ServiceHost();
        var runs = new int[3];
        host.Get<ITestService>(_ => runs[0]++);
        host.Get<ITestService>(_ =>
        {
            runs[1]++;
            throw new InvalidOperationException("boom");
        });
        host.Get<ITestService>(_ => runs[2]++);
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());

        Assertions.AssertThrown(() => host.ExecutePendingCallbacks())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("boom");
        host.ExecutePendingCallbacks();

        Assertions.AssertThat(runs).IsEqual(new[] { 1, 1, 1 });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SeveralFailuresAreRethrownTogether()
    {
        var host = new ServiceHost();
        host.Get<ITestService>(_ => throw new InvalidOperationException("a"));
        host.Get<ITestService>(_ => throw new ArgumentException("b"));
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());

        AggregateException? caught = null;
        try
        {
            host.ExecutePendingCallbacks();
        }
        catch (AggregateException error)
        {
            caught = error;
        }

        Assertions.AssertThat(caught).IsNotNull();
        Assertions.AssertInt(caught!.InnerExceptions.Count).IsEqual(2);
        Assertions.AssertThat(caught.InnerExceptions[0])
            .IsInstanceOf<InvalidOperationException>();
        Assertions.AssertThat(caught.InnerExceptions[1])
            .IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ThrowingTypeDoesNotStarveOtherRegisteredTypes()
    {
        var host = new ServiceHost();
        var otherRuns = 0;
        host.Get<IAsyncTestService>(
            _ => throw new InvalidOperationException("boom"));
        host.Get<ITestService>(_ => otherRuns++);
        host.Container.RegisterSingleton<IAsyncTestService>(
            new AsyncTestServiceImpl());
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());

        Assertions.AssertThrown(() => host.ExecutePendingCallbacks())
            .IsInstanceOf<InvalidOperationException>();

        Assertions.AssertInt(otherRuns).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ThrowingCallbackIsReportedThroughILogError()
    {
        var host = new ServiceHost();
        host.Get<ITestService>(
            _ => throw new InvalidOperationException("boom"));
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());
        var log = new RecordingLog();
        var previous = ILog.Sink;
        ILog.Sink = log;
        try
        {
            Assertions.AssertThrown(() => host.ExecutePendingCallbacks())
                .IsInstanceOf<InvalidOperationException>();
        }
        finally
        {
            ILog.Sink = previous;
        }

        Assertions.AssertInt(log.Errors.Count).IsEqual(1);
        Assertions.AssertBool(log.Errors[0].Contains("ITestService")).IsTrue();
        Assertions.AssertBool(log.Errors[0].Contains("boom")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CallbacksOfOneTypeRunInRegistrationOrder()
    {
        var host = new ServiceHost();
        var order = new List<int>();
        host.Get<ITestService>(_ => order.Add(1));
        host.Get<ITestService>(_ => order.Add(2));
        host.Get<ITestService>(_ => order.Add(3));
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());

        host.ExecutePendingCallbacks();

        Assertions.AssertThat(order).IsEqual(new List<int> { 1, 2, 3 });
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CallbackThatGetsARegisteredServiceRunsItOnce()
    {
        var host = new ServiceHost();
        var innerRuns = 0;
        host.Container.RegisterSingleton<IAsyncTestService>(
            new AsyncTestServiceImpl());
        host.Get<ITestService>(
            _ => host.Get<IAsyncTestService>(inner => innerRuns++));
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());

        host.ExecutePendingCallbacks();
        Assertions.AssertInt(innerRuns).IsEqual(1);

        host.ExecutePendingCallbacks();
        Assertions.AssertInt(innerRuns).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void
        CallbackThatGetsAnUnregisteredServiceRunsItOnTheNextCall()
    {
        var host = new ServiceHost();
        var innerRuns = 0;
        host.Get<ITestService>(
            _ => host.Get<IAsyncTestService>(inner => innerRuns++));
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());

        host.ExecutePendingCallbacks();
        Assertions.AssertInt(innerRuns).IsEqual(0);

        host.Container.RegisterSingleton<IAsyncTestService>(
            new AsyncTestServiceImpl());
        host.ExecutePendingCallbacks();
        Assertions.AssertInt(innerRuns).IsEqual(1);

        host.ExecutePendingCallbacks();
        Assertions.AssertInt(innerRuns).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CallbackThatExecutesPendingCallbacksDoesNotRerunItsType()
    {
        var host = new ServiceHost();
        var runs = 0;
        // The guard keeps the old behavior (a nested call re-running the
        // callback) from recursing forever; it only nests on the first run.
        host.Get<ITestService>(_ =>
        {
            if (runs++ == 0)
                host.ExecutePendingCallbacks();
        });
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());

        host.ExecutePendingCallbacks();

        Assertions.AssertInt(runs).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void CallbackThatResetsTheHostLeavesNoStaleEntry()
    {
        var host = new ServiceHost();
        var laterRuns = 0;
        host.Get<ITestService>(_ => host.Reset());
        host.Get<IAsyncTestService>(_ => laterRuns++);
        host.Container.RegisterSingleton<ITestService>(new TestServiceImpl());
        host.Container.RegisterSingleton<IAsyncTestService>(
            new AsyncTestServiceImpl());

        host.ExecutePendingCallbacks();
        host.Container.RegisterSingleton<IAsyncTestService>(
            new AsyncTestServiceImpl());
        host.ExecutePendingCallbacks();

        Assertions.AssertInt(laterRuns).IsEqual(0);
    }

    private sealed class RecordingLog : ILog
    {
        public List<string> Errors { get; } = new();

        public void LogMessage(string message)
        {
        }

        public void LogWarning(string message)
        {
        }

        public void LogError(string message) => Errors.Add(message);

        public void LogError(string message, Exception exception) =>
            Errors.Add(message);
    }
}
