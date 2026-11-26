using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Player.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Composition;

/// <summary>
///     Composes the service container as the "Services" autoload and injects every IDependent node that enters
///     the tree. Any other instance logs an error and composes nothing. Owns one <see cref="ServiceHost" />.
///     The container has two scopes. The app scope (the [Service] scan and the core services) is composed once,
///     synchronously in _Ready, so the first scene's nodes can resolve it in their own _Ready. The scene scope (the
///     current scene's service providers) is registered after each scene is ready and dropped when it leaves.
/// </summary>
public partial class CompositionRoot : Node
{
    // Tests share this assembly; only game classes are scanned for [Service].
    private const string GameNamespace = "CardCleaner.Scripts";

    // Node services the last Compose added as children; the next Compose frees them before it builds new ones.
    private readonly List<Node> _ownedNodes = new();

    // Only the autoload subscribes to the tree's signals; _ExitTree must not disconnect handlers it never connected.
    private bool _injecting;

    // The scene whose providers the scene scope holds; its removal from the tree drops that scope.
    private Node? _scene;

    /// <summary>The container, pending callbacks and resolver of this root; tests read the autoload's.</summary>
    internal ServiceHost Host { get; } = new();

    public override void _Ready()
    {
        if (!IsAutoload())
        {
            ILog.Error($"CompositionRoot {Name} is not the Services autoload; it composes nothing");
            return;
        }

        var tree = GetTree();
        // Subscribed first: ComposeAppScope adds IDependent node services that must be injected as they enter.
        tree.NodeAdded += OnNodeAdded;
        tree.NodeRemoved += OnNodeRemoved;
        tree.SceneChanged += OnSceneChanged;
        _injecting = true;
        InjectExistingNodes();
        ComposeOnStartup();
    }

    // The app scope now, before the main scene's _Ready; the scene scope after it. The main scene is in the tree
    // but not ready yet, and its providers join their group in their own _Ready.
    internal void ComposeOnStartup()
    {
        ComposeAppScope();
        _scene = GetTree().CurrentScene;
        CallDeferred(MethodName.ComposeSceneScope);
    }

    public override void _ExitTree()
    {
        if (!_injecting)
            return;
        var tree = GetTree();
        tree.NodeAdded -= OnNodeAdded;
        tree.NodeRemoved -= OnNodeRemoved;
        tree.SceneChanged -= OnSceneChanged;
        _injecting = false;
    }

    // ChangeSceneToFile removes the outgoing scene at once and adds the new one at the end of the frame, so the
    // scope is always dropped before the next scene's nodes enter and queue callbacks.
    private void OnNodeRemoved(Node node)
    {
        if (node != _scene)
            return;
        _scene = null;
        Host.LeaveScene();
    }

    // Emitted once the new scene is in the tree and every node in it, providers included, has run _Ready.
    private void OnSceneChanged()
    {
        _scene = GetTree().CurrentScene;
        ComposeSceneScope();
    }

    // SceneTree emits node_added after the node's _EnterTree and before its children enter and before its _Ready,
    // which is the timing the IDependent contract promises.
    private void OnNodeAdded(Node node)
    {
        if (node is IDependent dependent)
            dependent.Inject(Host);
    }

    // Injects once every IDependent node that entered the tree before the autoload subscribed to NodeAdded. The
    // main scene has entered the tree when Services gets _Ready, so the pass injects its nodes before their _Ready.
    // They can resolve app-scope services directly; a scene-scope service needs Get<T>(callback), like any scene's.
    internal void InjectExistingNodes() => InjectSubtree(GetTree().Root);

    private void InjectSubtree(Node node)
    {
        OnNodeAdded(node);
        foreach (var child in node.GetChildren())
            InjectSubtree(child);
    }

    // The sink mirrors the ILog in the host's current container, the same source that Has and Get read.
    private void SyncLogSink()
    {
        var container = Host.Container;
        ILog.Sink = container.IsRegistered<ILog>() ? container.Resolve<ILog>() : null;
    }

    private bool IsAutoload() => GetParent() == GetTree().Root && Name == "Services";

    // Internal so that TestServices can compose both scopes again on the autoload synchronously.
    internal void Compose()
    {
        ComposeAppScope();
        ComposeSceneScope();
    }

    internal void ComposeAppScope()
    {
        FreeOwnedNodes();
        // First, so the explicit registrations below replace any scanned entry for the same service type.
        RegisterGameServiceClasses(Host.Container);
        RegisterCoreServices(Host.Container);
        Host.Container.SealAppScope();
        Host.ExecutePendingCallbacks();
    }

    // Registers the providers of the scene in the tree, then runs the callbacks that waited for them. Providers
    // register the same nodes again when called twice, so a repeated pass changes nothing.
    internal void ComposeSceneScope()
    {
        RegisterFromProviders(Host.Container);
        Host.ExecutePendingCallbacks();
    }

    /// <summary>Registers the plain [Service] classes of the game lazily, the scan Compose runs.</summary>
    internal static void RegisterGameServiceClasses(IServiceContainer container) =>
        container.RegisterServiceClasses(
            ServiceClassScan.PlainServiceClassesIn(typeof(CompositionRoot).Assembly, GameNamespace));

    private void RegisterCoreServices(IServiceContainer container)
    {
        container.RegisterSingleton(RandomSources.Create());

        container.RegisterSingleton<ILog, LoggingService>();
        // Set before the services below are built: their constructors and _Ready already log through ILog.
        SyncLogSink();

        var inputService = new InputService();
        AddOwnedChild(inputService);
        container.RegisterSingleton<IInputService>(inputService);

        var assetPaths = new GodotAssetPaths();
        container.RegisterSingleton<IAssetPaths>(assetPaths);

        // The world data is read from files on its first resolve, on the main thread: the workshop in the shop
        // resolves the map generators and the tile registry in its _Ready. The main menu and the tests that never
        // build a workshop or a map never load it. The [Service] scan builds TileCatalogData and the map generators
        // from these entries.
        container.RegisterLazy<ITileRegistry>(
            () => TileCatalogLoad.Load(assetPaths, new CompiledAtlasSource()), typeof(ITileMetadataProvider));
        var biomeRegistry = new Lazy<BiomeRegistry>(() =>
        {
            var registry = new BiomeRegistry();
            registry.RegisterDefaultBiomes(assetPaths);
            return registry;
        });
        container.RegisterLazy<IBiomeCatalog>(() => biomeRegistry.Value);
        container.RegisterLazy(() => biomeRegistry.Value.ToBiomeSet());
        var transitionResolver = new Lazy<ITransitionResolver>(() => TransitionResolvers.Create(assetPaths));
        container.RegisterLazy(() => transitionResolver.Value);
        container.RegisterLazy(() => new TransitionPairs(transitionResolver.Value.GetAllTransitionPairs()));

        container.RegisterSingleton<IVisibilityChecker, SimpleVisibilityChecker>();
        container.RegisterSingleton(SafePositionTrackers.Create());

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
