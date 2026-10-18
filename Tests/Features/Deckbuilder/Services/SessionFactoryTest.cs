using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Tests.TestUtilities;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

/// <summary>
///     The session and combat factories are found by the [Service] scan and hand out independent instances.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class SessionFactoryTest
{
    private ServiceContainer _container = null!;

    [BeforeTest]
    public void Setup()
    {
        _container = new ServiceContainer();
        TestServices.RegisterGameServiceClasses(_container);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ScanRegistersBothFactories()
    {
        AssertThat(_container.Resolve<ISessionFactory>()).IsNotNull();
        AssertThat(_container.Resolve<ICombatSystemFactory>()).IsNotNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void EachWorldIsIndependent()
    {
        var factory = _container.Resolve<ISessionFactory>();

        var first = factory.CreateWorld();
        var second = factory.CreateWorld();

        AssertBool(ReferenceEquals(first, second)).IsFalse();
        AssertThat(first.EnemyCount).IsEqual(0);
        AssertThat(first.MapData).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void AnExplorationSessionWithoutAMapTakesNoStep()
    {
        var factory = _container.Resolve<ISessionFactory>();
        var exploration = factory.CreateExploration(factory.CreateWorld());

        AssertThat(exploration.MoveCount).IsEqual(0);
        AssertThat(exploration.Step()).IsEqual(ExplorationStepOutcome.Continuing);
    }
}
