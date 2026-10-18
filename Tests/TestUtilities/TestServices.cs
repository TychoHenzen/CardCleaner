using CardCleaner.Scripts.Composition;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
///     The single test entry point over the composition root's services: read or fill the container, rebuild or clear
///     it, and inject a node that a test builds outside the scene tree with the resolver the root injects. All of it
///     goes through the host of the "Services" autoload.
/// </summary>
public static class TestServices
{
    private static ServiceHost Host => Autoload.Host;

    /// <summary>The "Services" autoload, found through the main loop's root.</summary>
    public static CompositionRoot Autoload =>
        ((SceneTree)Engine.GetMainLoop()).Root.GetNode<CompositionRoot>("Services");

    public static IServiceContainer Container => Host.Container;

    public static IServiceResolver Resolver => Host;

    /// <summary>
    ///     Clears the container, then composes it again on the autoload synchronously. The new composition frees the
    ///     node services the previous one added.
    /// </summary>
    public static void Reinitialize()
    {
        Reset();
        Autoload.Compose();
    }

    /// <summary>
    ///     Replaces the host's container with an empty one, drops its pending callbacks and clears the log sink, which
    ///     no longer has a registered ILog to mirror. The host and the resolver injected from it stay.
    /// </summary>
    public static void Reset()
    {
        Host.Reset();
        ILog.Sink = null;
    }

    /// <summary>
    ///     Runs the queued <c>Get&lt;T&gt;(callback)</c> lookups whose service is now registered, as the root does
    ///     after composing.
    /// </summary>
    public static void ExecutePendingCallbacks() => Host.ExecutePendingCallbacks();

    /// <summary>Runs the composition root's [Service] scan of the game into the container.</summary>
    public static void RegisterGameServiceClasses(IServiceContainer container) =>
        CompositionRoot.RegisterGameServiceClasses(container);

    /// <summary>Injects a node the way the root does when it enters the tree, and returns it.</summary>
    public static T Inject<T>(T node) where T : Node, IDependent
    {
        node.Inject(Resolver);
        return node;
    }

    /// <summary>
    ///     Builds a <see cref="GameSessionService"/> outside the tree, after registering the game's [Service] classes
    ///     into the container, so its session factories resolve even after <see cref="Reset"/> emptied the container.
    ///     The session is injected here; a later tree injection leaves it as it is.
    /// </summary>
    public static GameSessionService CreateGameSession()
    {
        RegisterGameServiceClasses(Container);
        return Inject(new GameSessionService());
    }
}
