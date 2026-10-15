using CardCleaner.Scripts.Composition;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Tests.TestUtilities;

/// <summary>
///     The single test entry point over the composition root's container: read or fill it, rebuild or clear it, and
///     inject a node that a test builds outside the scene tree with the resolver the root injects.
/// </summary>
public static class TestServices
{
    public static IServiceContainer Container => ServiceLocator.Container;

    public static IServiceResolver Resolver => LocatorServiceResolver.Instance;

    public static void Reinitialize() => CompositionRoot.ReinitializeServices();

    public static void Reset() => ServiceLocator.ResetForTesting();

    /// <summary>
    ///     Runs the queued <c>Get&lt;T&gt;(callback)</c> lookups whose service is now registered, as the root does
    ///     after composing.
    /// </summary>
    public static void ExecutePendingCallbacks() => ServiceLocator.ExecutePendingCallbacks();

    /// <summary>Runs the composition root's [Service] scan of the game into the container.</summary>
    public static void RegisterGameServiceClasses(IServiceContainer container) =>
        CompositionRoot.RegisterGameServiceClasses(container);

    /// <summary>Injects a node the way the root does when it enters the tree, and returns it.</summary>
    public static T Inject<T>(T node) where T : Node, IDependent
    {
        node.Inject(Resolver);
        return node;
    }
}
