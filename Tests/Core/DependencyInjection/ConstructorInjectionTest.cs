using System;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Mocking;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Core.DependencyInjection;

/// <summary>
///     The container builds plain classes through their greediest public constructor, names the missing service when
///     it cannot, and registers scanned [Service] classes lazily: only once their constructor can be satisfied (#273,
///     AC-3). The scan cases pass this suite's own classes, which the game scan never sees (it only reads
///     CardCleaner.Scripts).
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ConstructorInjectionTest
{
    private ServiceContainer _container = null!;

    [BeforeTest]
    public void Setup()
    {
        _container = new ServiceContainer();
    }

    [AfterTest]
    public static void TearDown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GreediestConstructorReceivesTheRegisteredServices()
    {
        var first = new TestServiceImpl();
        var second = new AsyncTestServiceImpl();
        _container.RegisterSingleton<ITestService>(first);
        _container.RegisterSingleton<IAsyncTestService>(second);

        var built = _container.Construct<TwoServiceConsumer>();

        Assertions.AssertThat(built.First).IsSame(first);
        Assertions.AssertThat(built.Second).IsSame(second);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void MissingServiceErrorNamesTheClassAndTheService()
    {
        _container.RegisterSingleton<ITestService>(new TestServiceImpl());

        Assertions.AssertThrown(() => _container.Construct<TwoServiceConsumer>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage(
                $"Cannot construct {nameof(TwoServiceConsumer)}: service {nameof(IAsyncTestService)} not registered");
    }

    [TestCase]
    [TestCategory("Unit")]
    public void GenericRegistrationBuildsThroughConstructorInjection()
    {
        var first = new TestServiceImpl();
        _container.RegisterSingleton<ITestService>(first);
        _container.RegisterSingleton<IAsyncTestService>(new AsyncTestServiceImpl());

        _container.RegisterSingleton<IConsumer, TwoServiceConsumer>();

        Assertions.AssertThat(((TwoServiceConsumer)_container.Resolve<IConsumer>()).First).IsSame(first);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ScannedClassRegistersOnceItsDependencyIsRegistered()
    {
        _container.RegisterServiceClasses(new[] { typeof(ScannedConsumer) });

        Assertions.AssertBool(_container.IsRegistered<IScannedA>()).IsFalse();
        Assertions.AssertThrown(() => _container.Resolve<IScannedA>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"Cannot construct {nameof(ScannedConsumer)}: service {nameof(ITestService)} not registered");

        var dependency = new TestServiceImpl();
        _container.RegisterSingleton<ITestService>(dependency);

        Assertions.AssertBool(_container.IsRegistered<IScannedA>()).IsTrue();
        var a = _container.Resolve<IScannedA>();
        Assertions.AssertThat(((ScannedConsumer)a).Dependency).IsSame(dependency);
        // One instance, built once, shared by both of its [Service] types.
        Assertions.AssertThat(_container.Resolve<IScannedB>()).IsSame(a);
        Assertions.AssertThat(_container.Resolve<IScannedA>()).IsSame(a);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ScannedClassWithoutTypesRegistersUnderItsInterfaces()
    {
        _container.RegisterServiceClasses(new[] { typeof(InterfaceOnlyService) });

        Assertions.AssertThat(_container.Resolve<IScannedA>()).IsInstanceOf<InterfaceOnlyService>();
        Assertions.AssertThat(_container.Resolve<IScannedB>()).IsSame(_container.Resolve<IScannedA>());
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ExplicitRegistrationReplacesAScannedEntry()
    {
        _container.RegisterServiceClasses(new[] { typeof(InterfaceOnlyService) });
        var explicitA = new InterfaceOnlyService();

        _container.RegisterSingleton<IScannedA>(explicitA);

        Assertions.AssertThat(_container.Resolve<IScannedA>()).IsSame(explicitA);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScanSkipsNodeServiceClasses()
    {
        // MoneyService is a [Service] Node: AutoServiceProvider registers the live node, the container never builds it.
        var found = ServiceClassScan.PlainServiceClassesIn(typeof(ILog).Assembly, "CardCleaner.Scripts").ToList();

        Assertions.AssertBool(found.Contains(typeof(MoneyService))).IsFalse();
        Assertions.AssertThat(found).Contains(typeof(LoggingService));
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DependencyCycleIsNeverRegistered()
    {
        _container.RegisterServiceClasses(new[] { typeof(CycleA), typeof(CycleB) });

        Assertions.AssertBool(_container.IsRegistered<ICycleA>()).IsFalse();
        Assertions.AssertThrown(() => _container.Resolve<ICycleA>()).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ScanReadsOnlyTheGivenNamespace()
    {
        var game = ServiceClassScan.PlainServiceClassesIn(typeof(ILog).Assembly, "CardCleaner.Scripts").ToList();
        var suite = ServiceClassScan.PlainServiceClassesIn(
            typeof(ILog).Assembly, typeof(ConstructorInjectionTest).Namespace!).ToList();

        Assertions.AssertBool(game.Contains(typeof(ScannedConsumer))).IsFalse();
        Assertions.AssertBool(game.Contains(typeof(InterfaceOnlyService))).IsFalse();
        Assertions.AssertThat(suite).Contains(typeof(ScannedConsumer), typeof(InterfaceOnlyService));
        Assertions.AssertBool(suite.Contains(typeof(LoggingService))).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void PendingCallbackRunsWhenAScannedServiceBecomesRegistered()
    {
        var runs = 0;
        TestServices.Container.RegisterServiceClasses(new[] { typeof(ScannedConsumer) });
        TestServices.Resolver.Get<IScannedA>(_ => runs++);

        ServiceLocator.ExecutePendingCallbacks();
        Assertions.AssertInt(runs).IsEqual(0);

        TestServices.Container.RegisterSingleton<ITestService>(new TestServiceImpl());
        ServiceLocator.ExecutePendingCallbacks();
        Assertions.AssertInt(runs).IsEqual(1);
    }

    public interface IConsumer;

    public interface IScannedA;

    public interface IScannedB;

    public interface ICycleA;

    public interface ICycleB;

    public sealed class TwoServiceConsumer : IConsumer
    {
        // The parameterless constructor proves the container picks the greediest one.
        public TwoServiceConsumer()
        {
        }

        public TwoServiceConsumer(ITestService first, IAsyncTestService second)
        {
            First = first;
            Second = second;
        }

        public ITestService? First { get; }
        public IAsyncTestService? Second { get; }
    }

    [Service(typeof(IScannedA), typeof(IScannedB))]
    public sealed class ScannedConsumer(ITestService dependency) : IScannedA, IScannedB
    {
        public ITestService Dependency { get; } = dependency;
    }

    [Service]
    public sealed class InterfaceOnlyService : IScannedA, IScannedB;

    [Service(typeof(ICycleA))]
    public sealed class CycleA(ICycleB other) : ICycleA
    {
        public ICycleB Other { get; } = other;
    }

    [Service(typeof(ICycleB))]
    public sealed class CycleB(ICycleA other) : ICycleB
    {
        public ICycleA Other { get; } = other;
    }
}
