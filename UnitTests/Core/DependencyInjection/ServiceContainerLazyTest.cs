using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using GdUnit4;

namespace CardCleaner.Tests.Core.DependencyInjection;

/// <summary>
///     RegisterLazy: the composition root registers the world data (tile registry, biomes, transitions) this way, so
///     a run that never generates a world never reads those files.
/// </summary>
[TestSuite]
public class ServiceContainerLazyTest
{
    private ServiceContainer _container = null!;
    private int _builds;

    [BeforeTest]
    public void Setup()
    {
        _container = new ServiceContainer();
        _builds = 0;
    }

    private Data Build()
    {
        _builds++;
        return new Data();
    }

    [TestCase]
    public void NothingIsBuiltUntilTheFirstResolve()
    {
        _container.RegisterLazy<IData>(Build);

        Assertions.AssertBool(_container.IsRegistered<IData>()).IsTrue();
        Assertions.AssertInt(_builds).IsEqual(0);
    }

    [TestCase]
    public void TheFactoryRunsOnceAndEveryResolveSharesItsInstance()
    {
        _container.RegisterLazy<IData>(Build);

        var first = _container.Resolve<IData>();

        Assertions.AssertThat(_container.Resolve<IData>()).IsSame(first);
        Assertions.AssertInt(_builds).IsEqual(1);
    }

    [TestCase]
    public void TheExtraServiceTypesShareTheOneInstance()
    {
        _container.RegisterLazy<IData>(Build, typeof(IOtherView));

        var asData = _container.Resolve<IData>();

        Assertions.AssertThat(_container.Resolve<IOtherView>()).IsSame(asData);
        Assertions.AssertInt(_builds).IsEqual(1);
    }

    [TestCase]
    public void AnInstanceThatIsNotAnExtraServiceTypeFailsNamingThatType()
    {
        _container.RegisterLazy<IData>(() => new DataOnly(), typeof(IOtherView));

        Assertions.AssertThrown(() => _container.Resolve<IOtherView>())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("The lazy IData factory built DataOnly, which is not a IOtherView");
    }

    [TestCase]
    public void AnExplicitRegistrationReplacesItWithoutBuildingIt()
    {
        _container.RegisterLazy<IData>(Build);
        var explicitData = new Data();

        _container.RegisterSingleton<IData>(explicitData);

        Assertions.AssertThat(_container.Resolve<IData>()).IsSame(explicitData);
        Assertions.AssertInt(_builds).IsEqual(0);
    }

    [TestCase]
    public void ItReplacesAnEarlierExplicitRegistration()
    {
        _container.RegisterSingleton<IData>(new Data());

        _container.RegisterLazy<IData>(Build);

        _container.Resolve<IData>();
        Assertions.AssertInt(_builds).IsEqual(1);
    }

    [TestCase]
    public void AScannedClassCountsItAsRegisteredAndIsBuiltFromIt()
    {
        _container.RegisterServiceClasses(new[] { typeof(Consumer) });
        _container.RegisterLazy<IData>(Build);

        Assertions.AssertBool(_container.IsRegistered<IConsumer>()).IsTrue();
        var consumer = _container.Resolve<IConsumer>();

        Assertions.AssertThat(consumer.Data).IsSame(_container.Resolve<IData>());
        Assertions.AssertInt(_builds).IsEqual(1);
    }

    [TestCase]
    public void AnAppScopeEntryKeepsItsInstanceAcrossASceneDrop()
    {
        _container.RegisterLazy<IData>(Build);
        _container.SealAppScope();
        var first = _container.Resolve<IData>();

        _container.DropSceneScope();

        Assertions.AssertThat(_container.Resolve<IData>()).IsSame(first);
        Assertions.AssertInt(_builds).IsEqual(1);
    }

    [TestCase]
    public void AFactoryThatThrowsIsTriedAgainOnTheNextResolve()
    {
        var fail = true;
        _container.RegisterLazy<IData>(() =>
        {
            _builds++;
            if (fail)
                throw new InvalidOperationException("file missing");
            return new Data();
        });

        Assertions.AssertThrown(() => _container.Resolve<IData>()).IsInstanceOf<InvalidOperationException>();
        fail = false;

        Assertions.AssertThat(_container.Resolve<IData>()).IsNotNull();
        Assertions.AssertInt(_builds).IsEqual(2);
    }

    public interface IData;

    public interface IOtherView;

    public interface IConsumer
    {
        IData Data { get; }
    }

    private sealed class Data : IData, IOtherView;

    private sealed class DataOnly : IData;

    [Service(typeof(IConsumer))]
    private sealed class Consumer(IData data) : IConsumer
    {
        public IData Data { get; } = data;
    }
}
