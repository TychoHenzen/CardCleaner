using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Player.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Core.Randomness;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Composition;

/// <summary>
///     Composes the service container as the "Services" autoload and injects every IDependent node that enters
///     the tree. Any other instance logs an error and composes nothing. Owns one <see cref="ServiceHost" />.
/// </summary>
public partial class CompositionRoot : Node
{
    // Tests share this assembly; only game classes are scanned for [Service].
    private const string GameNamespace = "CardCleaner.Scripts";

    // Node services the last Compose added as children; the next Compose frees them before it builds new ones.
    private readonly List<Node> _ownedNodes = new();

    // Only the autoload subscribes to NodeAdded; _ExitTree must not disconnect a handler it never connected.
    private bool _injecting;

    /// <summary>The container, pending callbacks and resolver of this root; tests read the autoload's.</summary>
    internal ServiceHost Host { get; } = new();

    public override void _Ready()
    {
        if (!IsAutoload())
        {
            ILog.Error($"CompositionRoot {Name} is not the Services autoload; it composes nothing");
            return;
        }

        GetTree().NodeAdded += OnNodeAdded;
        _injecting = true;
        InjectExistingNodes();
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
            dependent.Inject(Host);
    }

    // Injects once every IDependent node that entered the tree before the autoload subscribed to NodeAdded. That
    // runs after such a node's _Ready. Services is the first autoload, so today the pass meets only the root and
    // Services itself; it guards a node that a later change orders before Services.
    internal void InjectExistingNodes() => InjectSubtree(GetTree().Root);

    private void InjectSubtree(Node node)
    {
        OnNodeAdded(node);
        foreach (var child in node.GetChildren())
            InjectSubtree(child);
    }

    // ASSUMPTION: the deferred call runs after the main scene's service providers enter the tree, as the
    // removed locator's _Ready call did before the split. No requirement states this ordering.
    internal void ScheduleCompose() => CallDeferred(MethodName.Compose);

    // The sink mirrors the ILog in the host's current container, the same source that Has and Get read.
    private void SyncLogSink()
    {
        var container = Host.Container;
        ILog.Sink = container.IsRegistered<ILog>() ? container.Resolve<ILog>() : null;
    }

    private bool IsAutoload() => GetParent() == GetTree().Root && Name == "Services";

    // Internal so that TestServices can compose again on the autoload synchronously.
    internal void Compose()
    {
        FreeOwnedNodes();
        // First, so the explicit registrations below replace any scanned entry for the same service type.
        RegisterGameServiceClasses(Host.Container);
        RegisterCoreServices(Host.Container);
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

        var tileRegistry = TileCatalogLoad.Load(assetPaths, new CompiledAtlasSource());
        container.RegisterSingleton<ITileRegistry>(tileRegistry);
        container.RegisterSingleton<ITileMetadataProvider>(tileRegistry);
        // The [Service] scan builds TileCatalogData once from the registry above; the root only resolves it.
        _ = container.Resolve<ITileCatalog>();

        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes(assetPaths);
        container.RegisterSingleton<IBiomeCatalog>(biomeRegistry);
        // The data the map generators take as constructor parameters; the [Service] scan builds the generators from them.
        var biomes = biomeRegistry.ToBiomeSet();
        container.RegisterSingleton(biomes);
        var transitionResolver = TransitionResolvers.Create(assetPaths);
        container.RegisterSingleton(transitionResolver);
        var transitionPairs = new TransitionPairs(transitionResolver.GetAllTransitionPairs());
        container.RegisterSingleton(transitionPairs);

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
