using CardCleaner.Scripts.Core.DependencyInjection;
using GdUnit4;

namespace CardCleaner.Tests.Core.DependencyInjection;

/// <summary>
///     The container's two scopes: what SealAppScope recorded outlives a scene, and DropSceneScope removes what a
///     scene registered and rebuilds the scanned classes that were built from it. These model the save slot the menu
///     activates (app scope, must reach the shop) and the shop's bill book (built from shop nodes, must not reach the
///     next shop).
/// </summary>
[TestSuite]
public class ServiceContainerScopeTest
{
    private ServiceContainer _container = null!;

    [BeforeTest]
    public void Setup()
    {
        _container = new ServiceContainer();
    }

    [TestCase]
    public void ASceneRegistrationIsGoneAfterTheDrop()
    {
        _container.SealAppScope();
        _container.RegisterSingleton<ISceneNode>(new SceneNode());

        _container.DropSceneScope();

        Assertions.AssertBool(_container.IsRegistered<ISceneNode>()).IsFalse();
    }

    [TestCase]
    public void AnAppRegistrationThatASceneReplacedComesBack()
    {
        var app = new SceneNode();
        _container.RegisterSingleton<ISceneNode>(app);
        _container.SealAppScope();
        _container.RegisterSingleton<ISceneNode>(new SceneNode());

        _container.DropSceneScope();

        Assertions.AssertThat(_container.Resolve<ISceneNode>()).IsSame(app);
    }

    [TestCase]
    public void AScannedClassBuiltOnlyFromTheAppScopeKeepsItsInstance()
    {
        _container.RegisterServiceClasses(new[] { typeof(AppOnly) });
        _container.SealAppScope();
        var first = _container.Resolve<IAppOnly>();

        _container.DropSceneScope();

        Assertions.AssertThat(_container.Resolve<IAppOnly>()).IsSame(first);
    }

    [TestCase]
    public void AScannedClassBuiltFromASceneServiceIsBuiltAgainForTheNextScene()
    {
        _container.RegisterServiceClasses(new[] { typeof(SceneConsumer) });
        _container.SealAppScope();
        var firstNode = new SceneNode();
        _container.RegisterSingleton<ISceneNode>(firstNode);
        var first = _container.Resolve<ISceneConsumer>();

        _container.DropSceneScope();
        var waitsForTheNextScene = !_container.IsRegistered<ISceneConsumer>();
        var secondNode = new SceneNode();
        _container.RegisterSingleton<ISceneNode>(secondNode);
        var second = _container.Resolve<ISceneConsumer>();

        Assertions.AssertBool(waitsForTheNextScene).IsTrue();
        Assertions.AssertThat(first.Node).IsSame(firstNode);
        Assertions.AssertThat(second.Node).IsSame(secondNode);
    }

    [TestCase]
    public void AScannedClassBuiltFromAnotherSceneBoundClassIsBuiltAgainToo()
    {
        _container.RegisterServiceClasses(new[] { typeof(SceneConsumer), typeof(IndirectConsumer) });
        _container.SealAppScope();
        _container.RegisterSingleton<ISceneNode>(new SceneNode());
        var first = _container.Resolve<IIndirectConsumer>();

        _container.DropSceneScope();
        var secondNode = new SceneNode();
        _container.RegisterSingleton<ISceneNode>(secondNode);
        var second = _container.Resolve<IIndirectConsumer>();

        Assertions.AssertThat(second).IsNotSame(first);
        Assertions.AssertThat(second.Inner.Node).IsSame(secondNode);
    }

    [TestCase]
    public void DropBeforeSealChangesNothing()
    {
        var node = new SceneNode();
        _container.RegisterSingleton<ISceneNode>(node);

        _container.DropSceneScope();

        Assertions.AssertThat(_container.Resolve<ISceneNode>()).IsSame(node);
    }

    public interface ISceneNode
    {
    }

    public interface IAppOnly
    {
    }

    public interface ISceneConsumer
    {
        ISceneNode Node { get; }
    }

    public interface IIndirectConsumer
    {
        ISceneConsumer Inner { get; }
    }

    private sealed class SceneNode : ISceneNode
    {
    }

    [Service(typeof(IAppOnly))]
    private sealed class AppOnly : IAppOnly
    {
    }

    [Service(typeof(ISceneConsumer))]
    private sealed class SceneConsumer(ISceneNode node) : ISceneConsumer
    {
        public ISceneNode Node { get; } = node;
    }

    [Service(typeof(IIndirectConsumer))]
    private sealed class IndirectConsumer(ISceneConsumer inner) : IIndirectConsumer
    {
        public ISceneConsumer Inner { get; } = inner;
    }
}
