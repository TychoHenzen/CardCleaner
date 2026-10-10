using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Player.Services;
using Godot;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
///     Global service locator that integrates with Godot's autoload system.
///     Add this as an autoload named "Services" in project settings.
/// </summary>
public partial class ServiceLocator : Node
{
    private static ServiceLocator _instance = null!;

    // Static pending callbacks for when Get<T>(callback) is called before _instance exists
    private static readonly Dictionary<Type, List<Action<object>>> StaticPendingCallbacks = new();
    private readonly Dictionary<Type, List<Action<object>>> _pendingCallbacks = new();
    private IServiceContainer _container = new ServiceContainer();

    // The instance this node replaced in _Ready. _ExitTree restores it while this node is still the instance and
    // the replaced instance is still valid (GodotObject.IsInstanceValid).
    private ServiceLocator? _replacedInstance;


    public static IServiceContainer Container => _instance._container;

    public override void _Ready()
    {
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_instance != null)
            ILog.Error("!!!Duplicate service locator!!!");
        _replacedInstance = _instance;
        _instance = this;

        // Transfer any callbacks that were registered before _instance existed
        foreach (var kvp in StaticPendingCallbacks)
        {
            if (!_pendingCallbacks.TryGetValue(kvp.Key, out var list))
            {
                list = new List<Action<object>>();
                _pendingCallbacks[kvp.Key] = list;
            }

            list.AddRange(kvp.Value);
        }

        StaticPendingCallbacks.Clear();

        CallDeferred(MethodName.ResolveServices);
    }

    public override void _ExitTree()
    {
        // ASSUMPTION: with no live replaced instance (only the autoload, which exits at quit), the static is kept.
        // A replaced instance freed earlier is not walked past. No requirement states either case.
        var replaced = _replacedInstance;
        if (ReferenceEquals(_instance, this) && replaced != null && GodotObject.IsInstanceValid(replaced))
            _instance = replaced;
        _replacedInstance = null;
    }

    public static void ReinitializeServices()
    {
        ResetForTesting();
        _instance.ResolveServices();
    }

    public static void ResetForTesting()
    {
        _instance._container = new ServiceContainer();
        _instance._pendingCallbacks.Clear();
    }

    private void ResolveServices()
    {
        // Register core services
        RegisterCoreServices();

        // Find and register services from providers
        RegisterFromProviders();
        // Execute any pending callbacks after all services are registered
        ExecutePendingCallbacks();
    }

    private void RegisterCoreServices()
    {
        _container.RegisterSingleton(new RandomNumberGenerator());

        _container.RegisterSingleton<ILog, LoggingService>();

        var inputService = new InputService();
        AddChild(inputService);
        _container.RegisterSingleton<IInputService>(inputService);

        // Register TileRegistry as both ITileRegistry and ITileMetadataProvider
        var tileRegistry = new TileRegistry();
        _container.RegisterSingleton<ITileRegistry>(tileRegistry);
        _container.RegisterSingleton<ITileMetadataProvider>(tileRegistry);

        _container.RegisterSingleton<IVisibilityChecker, SimpleVisibilityChecker>();
        _container.RegisterSingleton<ISafePositionTracker, SafePositionTracker>();

        var playerResetService = new PlayerResetService();
        AddChild(playerResetService);
        _container.RegisterSingleton<IPlayerResetService>(playerResetService);
    }

    private void RegisterFromProviders()
    {
        var providers = GetTree().GetNodesInGroup("service_providers");
        foreach (var node in providers)
            if (node is IServiceProvider provider)
                provider.RegisterServices(_container);
    }

    public static void ExecutePendingCallbacks()
    {
        foreach (var keyValuePair in _instance._pendingCallbacks.ToList())
        {
            if (!_instance._container.IsRegistered(keyValuePair.Key))
                continue;

            var service = _instance._container.Resolve(keyValuePair.Key);
            foreach (var callback in keyValuePair.Value)
            {
                ILog.Print($"Running callback for {keyValuePair.Key.Name}");
                callback(service);
            }

            ILog.Print($"Removing callback for {keyValuePair.Key.Name}");
            _instance._pendingCallbacks.Remove(keyValuePair.Key);
        }
    }

    public static T Get<T>() where T : class
    {
        return Container.Resolve<T>();
    }

    public static void Get<T>(Action<T> callback) where T : class
    {
        var serviceType = typeof(T);

        // If _instance doesn't exist yet, queue in static callbacks
        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
        if (_instance == null)
        {
            if (!StaticPendingCallbacks.TryGetValue(serviceType, out var staticList))
            {
                staticList = new List<Action<object>>();
                StaticPendingCallbacks[serviceType] = staticList;
            }

            staticList.Add(obj => callback((T)obj));
            return;
        }

        // If service is already available, call callback immediately
        if (Container.IsRegistered<T>())
        {
            callback(Container.Resolve<T>());
            return;
        }

        // Otherwise, store callback for later execution
        if (!_instance._pendingCallbacks.TryGetValue(serviceType, out var value))
        {
            value = new List<Action<object>>();
            _instance._pendingCallbacks[serviceType] = value;
        }

        value.Add(obj => callback((T)obj));
    }

    public static bool Has<T>() where T : class
    {
        return _instance != null && Container.IsRegistered<T>();
    }
}
