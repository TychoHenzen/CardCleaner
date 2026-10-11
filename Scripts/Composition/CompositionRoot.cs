using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Player.Services;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Composition;

/// <summary>
///     Composes the service container. Registered as the "Services" autoload, it registers the core services, then
///     every node in the "service_providers" group, then runs the pending callbacks. Only that autoload composes; any
///     other instance logs an error and leaves ServiceLocator untouched.
/// </summary>
public partial class CompositionRoot : Node
{
    public override void _Ready()
    {
        if (!IsAutoload())
        {
            ILog.Error($"CompositionRoot {Name} is not the Services autoload; it composes nothing");
            return;
        }

        // ASSUMPTION: the deferred call runs after the main scene's service providers enter the tree, as the
        // ServiceLocator._Ready call did before the split. No requirement states this ordering.
        CallDeferred(MethodName.Compose);
    }

    /// <summary>
    ///     Test-only: clears the container, then composes it again on the autoload synchronously.
    /// </summary>
    public static void ReinitializeServices()
    {
        ServiceLocator.ResetForTesting();
        ((SceneTree)Engine.GetMainLoop()).Root.GetNode<CompositionRoot>("Services").Compose();
    }

    private bool IsAutoload() => GetParent() == GetTree().Root && Name == "Services";

    private void Compose()
    {
        RegisterCoreServices(ServiceLocator.Container);
        RegisterFromProviders(ServiceLocator.Container);
        ServiceLocator.ExecutePendingCallbacks();
    }

    private void RegisterCoreServices(IServiceContainer container)
    {
        container.RegisterSingleton(new RandomNumberGenerator());

        container.RegisterSingleton<ILog, LoggingService>();
        // Set before the services below are built: their constructors and _Ready already log through ILog.
        ServiceLocator.SyncLogSink();

        var inputService = new InputService();
        AddChild(inputService);
        container.RegisterSingleton<IInputService>(inputService);

        // Register TileRegistry as both ITileRegistry and ITileMetadataProvider
        var tileRegistry = new TileRegistry();
        container.RegisterSingleton<ITileRegistry>(tileRegistry);
        container.RegisterSingleton<ITileMetadataProvider>(tileRegistry);

        container.RegisterSingleton<IVisibilityChecker, SimpleVisibilityChecker>();
        container.RegisterSingleton<ISafePositionTracker, SafePositionTracker>();

        var playerResetService = new PlayerResetService();
        AddChild(playerResetService);
        container.RegisterSingleton<IPlayerResetService>(playerResetService);
    }

    private void RegisterFromProviders(IServiceContainer container)
    {
        var providers = GetTree().GetNodesInGroup("service_providers");
        foreach (var node in providers)
            if (node is IServiceProvider provider)
                provider.RegisterServices(container);
    }
}
