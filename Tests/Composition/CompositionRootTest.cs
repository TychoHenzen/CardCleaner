using System;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Composition;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Tests.Core.DependencyInjection;
using CardCleaner.Tests.Mocking;
using Godot;
using NSubstitute;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Composition;

/// <summary>
/// The "Services" autoload composes the container: in the game, in the deferred-call flush after its _Ready; in tests,
/// synchronously through TestServices.Reinitialize. Any other CompositionRoot logs an error and changes nothing (#168).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CompositionRootTest
{
    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestAutoloadRootRegistersServiceProvidersOnReinitialize()
    {
        // Arrange - a provider in the service_providers group, in the tree the way a scene provider is
        var provider = Assertions.AddNode(new TestServiceProvider());
        provider.AddToGroup("service_providers");
        var created = Array.Empty<Node>();
        try
        {
            // Act - the autoload composes synchronously, which calls RegisterServices on every provider in the group
            TestServices.Reinitialize();
            created = ReinitializedNodes();

            // Assert
            Assertions.AssertBool(provider.RegisterServicesCalled).IsTrue();
            Assertions.AssertBool(TestServices.Resolver.Has<IAsyncTestService>()).IsTrue();
        }
        finally
        {
            QueueFreeAll(created);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task TestSecondRootLeavesContainerAndSinkUnchanged()
    {
        // Arrange - the autoload has composed; a recording sink stands in for its logger so the error can be read,
        // and a callback waits for a service nobody has registered
        TestServices.Reinitialize();
        var created = ReinitializedNodes();
        var composedSink = ILog.Sink;
        var recorder = Substitute.For<ILog>();
        ILog.Sink = recorder;
        var container = TestServices.Container;
        var log = TestServices.Resolver.Get<ILog>();
        var input = TestServices.Resolver.Get<IInputService>();
        var callbackRuns = 0;
        TestServices.Resolver.Get<ITestService>(_ => callbackRuns++);
        var second = Assertions.AddNode(new CompositionRoot(), autoFree: false);
        try
        {
            // Act - the second root enters the tree
            await second.ToSignal(second.GetTree(), SceneTree.SignalName.ProcessFrame);

            // Assert - it reported itself and changed nothing; the callback is still queued and runs once later
            recorder.Received(1).LogError(Arg.Is<string>(message => message.Contains("is not the Services autoload")));
            Assertions.AssertThat(ILog.Sink).IsSame(recorder);
            Assertions.AssertThat(TestServices.Container).IsSame(container);
            Assertions.AssertThat(TestServices.Resolver.Get<ILog>()).IsSame(log);
            Assertions.AssertThat(TestServices.Resolver.Get<IInputService>()).IsSame(input);
            Assertions.AssertInt(callbackRuns).IsEqual(0);
            TestServices.Container.RegisterSingleton<ITestService>(new TestServiceImpl());
            TestServices.ExecutePendingCallbacks();
            Assertions.AssertInt(callbackRuns).IsEqual(1);
        }
        finally
        {
            // The second root composed nothing, so only it and the nodes the autoload created need freeing
            ILog.Sink = composedSink;
            second.GetParent()?.RemoveChild(second);
            second.Free();
            QueueFreeAll(created);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static async Task TestAutoloadComposesInTheDeferredFlushAfterReady()
    {
        // Arrange - an empty container, as it is before the autoload's first composition
        TestServices.Reset();
        var root = TestServices.Autoload;
        var created = Array.Empty<Node>();
        try
        {
            // Act - schedule the way _Ready does, then add a provider the way a main-scene provider joins in its _Ready
            root.ScheduleCompose();
            var provider = Assertions.AddNode(new TestServiceProvider());
            provider.AddToGroup("service_providers");
            var composedBeforeTheFlush = TestServices.Resolver.Has<ILog>();
            // Deferred calls run in the order they were queued, so this one completes after the scheduled Compose
            var flushed = new TaskCompletionSource();
            Callable.From(() => flushed.SetResult()).CallDeferred();
            await flushed.Task;
            created = ReinitializedNodes();

            // Assert
            Assertions.AssertBool(composedBeforeTheFlush).IsFalse();
            Assertions.AssertBool(provider.RegisterServicesCalled).IsTrue();
            Assertions.AssertBool(TestServices.Resolver.Has<IAsyncTestService>()).IsTrue();
        }
        finally
        {
            QueueFreeAll(created);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestReinitializeLeavesOneLiveNodeServiceOfEachKind()
    {
        // Act - compose twice; the second composition must release the node services the first one added
        TestServices.Reinitialize();
        TestServices.Reinitialize();
        var created = ReinitializedNodes();
        try
        {
            // Assert - queued nodes stay children until the frame ends, so only unqueued ones count as live
            var live = TestServices.Autoload.GetChildren().Where(node => !node.IsQueuedForDeletion()).ToList();
            Assertions.AssertInt(live.OfType<IInputService>().Count()).IsEqual(1);
            Assertions.AssertInt(live.OfType<IPlayerResetService>().Count()).IsEqual(1);
        }
        finally
        {
            QueueFreeAll(created);
        }
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TestReinitializeRegistersTheMeshMapGenerator()
    {
        // Arrange and act - the autoload composes the core services, the irregular map's generator among them
        TestServices.Reinitialize();
        var created = ReinitializedNodes();
        try
        {
            // Assert - the cabinet resolves the real generator for the irregular kind, not a test double
            Assertions.AssertThat(TestServices.Resolver.Get<IMeshMapGenerator>()).IsInstanceOf<MeshMapGenerator>();
        }
        finally
        {
            QueueFreeAll(created);
        }
    }

    // Reinitialize adds an input service and a player reset service to the autoload; the tests free them.
    private static Node[] ReinitializedNodes() =>
        new[]
        {
            (Node)TestServices.Resolver.Get<IInputService>(), (Node)TestServices.Resolver.Get<IPlayerResetService>()
        };

    private static void QueueFreeAll(Node[] nodes)
    {
        foreach (var node in nodes)
            node.QueueFree();
    }
}
