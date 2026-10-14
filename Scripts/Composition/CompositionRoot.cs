using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Player.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Composition;

/// <summary>
///     Composes the service container. Registered as the "Services" autoload, it registers the core services, then
///     every node in the "service_providers" group, then runs the pending callbacks. Before that it registers the
///     plain [Service] classes of the game lazily. From its _Ready on, it injects every IDependent node that enters
///     the tree. Only that autoload composes and injects; any other instance logs an error and leaves ServiceLocator
///     untouched.
/// </summary>
public partial class CompositionRoot : Node
{
    // Tests compile into the same assembly; only game classes are scanned for [Service].
    private const string GameNamespace = "CardCleaner.Scripts";

    // Node services the last Compose added as children; the next Compose frees them before it builds new ones.
    private readonly List<Node> _ownedNodes = new();

    // Only the autoload subscribes to NodeAdded; _ExitTree must not disconnect a handler it never connected.
    private bool _injecting;

    public override void _Ready()
    {
        if (!IsAutoload())
        {
            ILog.Error($"CompositionRoot {Name} is not the Services autoload; it composes nothing");
            return;
        }

        GetTree().NodeAdded += OnNodeAdded;
        _injecting = true;
        InjectSubtree(GetTree().Root);
        ScheduleCompose();
    }

    public override void _ExitTree()
    {
        if (!_injecting)
            return;
        GetTree().NodeAdded -= OnNodeAdded;
        _injecting = false;
    }

    // SceneTree emits node_added after the node's _EnterTree and before its children enter and before its _Ready,
    // which is the timing the IDependent contract promises.
    private void OnNodeAdded(Node node)
    {
        if (node is IDependent dependent)
            dependent.Inject(LocatorServiceResolver.Instance);
    }

    // Nodes that entered the tree before the autoload subscribed (other autoloads) are injected once here.
    private void InjectSubtree(Node node)
    {
        OnNodeAdded(node);
        foreach (var child in node.GetChildren())
            InjectSubtree(child);
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
        // First, so the explicit registrations below replace any scanned entry for the same service type.
        // ASSUMPTION: SignatureCardGenerator has a parameterless constructor until #273 task-2, so its scanned
        // ICardGenerator counts as registered in every composition; every scene with a CardSpawningService also has
        // the CardServiceProvider that registers it explicitly, so no shipped scene sees a difference.
        ServiceLocator.Container.RegisterServiceClasses(
            ServiceClassScan.PlainServiceClassesIn(typeof(CompositionRoot).Assembly, GameNamespace));
        RegisterCoreServices(ServiceLocator.Container);
        RegisterFromProviders(ServiceLocator.Container);
        ServiceLocator.ExecutePendingCallbacks();
    }

    private void RegisterCoreServices(IServiceContainer container)
    {
        container.RegisterSingleton(RandomSources.Create());

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
        // ASSUMPTION: the issues ask for [Service], but AutoServiceProvider only discovers Nodes in the current scene;
        // the generators are plain classes, so the composition root registers them.
        container.RegisterSingleton<IGridMapGenerator>(new GridMapGenerator(tileRegistry, tileRegistry, biomeRegistry));
        container.RegisterSingleton<IMeshMapGenerator>(new MeshMapGenerator(tileRegistry, biomeRegistry));

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
