using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Player.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
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
    // Node services the last Compose added as children; the next Compose frees them before it builds new ones.
    private readonly List<Node> _ownedNodes = new();

    public override void _Ready()
    {
        if (!IsAutoload())
        {
            ILog.Error($"CompositionRoot {Name} is not the Services autoload; it composes nothing");
            return;
        }

        ScheduleCompose();
    }

    // ASSUMPTION: the deferred call runs after the main scene's service providers enter the tree, as the
    // ServiceLocator._Ready call did before the split. No requirement states this ordering.
    internal void ScheduleCompose() => CallDeferred(MethodName.Compose);

    /// <summary>
    ///     Test-only: clears the container, then composes it again on the autoload synchronously. The new composition
    ///     frees the node services the previous one added.
    /// </summary>
    public static void ReinitializeServices()
    {
        ServiceLocator.ResetForTesting();
        ((SceneTree)Engine.GetMainLoop()).Root.GetNode<CompositionRoot>("Services").Compose();
    }

    private bool IsAutoload() => GetParent() == GetTree().Root && Name == "Services";

    private void Compose()
    {
        FreeOwnedNodes();
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
        AddOwnedChild(inputService);
        container.RegisterSingleton<IInputService>(inputService);

        var tileRegistry = new TileRegistry();
        container.RegisterSingleton<ITileRegistry>(tileRegistry);
        container.RegisterSingleton<ITileMetadataProvider>(tileRegistry);

        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes();
        // ASSUMPTION: the issue asks for [Service], but AutoServiceProvider only discovers Nodes in the current scene;
        // the generator is a plain class, so the composition root registers it.
        container.RegisterSingleton<IGridMapGenerator>(new GridMapGenerator(tileRegistry, tileRegistry, biomeRegistry));

        container.RegisterSingleton<IVisibilityChecker, SimpleVisibilityChecker>();
        container.RegisterSingleton<ISafePositionTracker, SafePositionTracker>();

        var playerResetService = new PlayerResetService();
        AddOwnedChild(playerResetService);
        container.RegisterSingleton<IPlayerResetService>(playerResetService);
    }

    private void AddOwnedChild(Node node)
    {
        AddChild(node);
        _ownedNodes.Add(node);
    }

    // A test may already have freed or queued these nodes itself, so only live, unqueued ones are freed here.
    private void FreeOwnedNodes()
    {
        foreach (var node in _ownedNodes)
            if (IsInstanceValid(node) && !node.IsQueuedForDeletion())
                node.QueueFree();
        _ownedNodes.Clear();
    }

    private void RegisterFromProviders(IServiceContainer container)
    {
        var providers = GetTree().GetNodesInGroup("service_providers");
        foreach (var node in providers)
            if (node is IServiceProvider provider)
                provider.RegisterServices(container);
    }
}
