using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
/// Global service locator that integrates with Godot's autoload system.
/// Add this as an autoload named "Services" in project settings.
/// </summary>
public partial class ServiceLocator : Node
{
    private static ServiceLocator _instance;
    private readonly IServiceContainer _container = new ServiceContainer();
    private readonly Dictionary<Type, List<Action<object>>> _pendingCallbacks = new();


    public static IServiceContainer Container => _instance._container;

    public override void _Ready()
    {
        _instance = this;

        CallDeferred(nameof(ResolveServices));
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
        // Register RandomNumberGenerator as singleton
        _container.RegisterSingleton<RandomNumberGenerator>(new RandomNumberGenerator());

        var inputService = new InputService();
        AddChild(inputService);
        _container.RegisterSingleton<IInputService>(inputService);
        
        // Register factories for Godot resources that need special handling
        _container.RegisterFactory<PackedScene>(() =>
            GD.Load<PackedScene>("res://Scenes/Card.tscn"));
    }

    private void RegisterFromProviders()
    {
        var providers = GetTree().GetNodesInGroup("service_providers");
        foreach (var node in providers)
        {
            if (node is IServiceProvider provider)
            {
                provider.RegisterServices(_container);
            }
        }
    }

    public static void ExecutePendingCallbacks()
    {
        foreach (var kvp in _instance._pendingCallbacks)
        {
            var serviceType = kvp.Key;
            var callbacks = kvp.Value;
            
            if (_instance._container.IsRegistered(serviceType))
            {
                var service = _instance._container.Resolve(serviceType);
                foreach (var callback in callbacks)
                {
                    GD.Print($"Running callback for {serviceType.Name}");
                    callback(service);
                }
                GD.Print($"Removing callback for {serviceType.Name}");
                _instance._pendingCallbacks.Remove(serviceType);
            }
            
        }
    }

    public static T Get<T>() where T : class => Container.Resolve<T>();
    public static void Get<T>(Action<T> callback) where T : class
    {
        var serviceType = typeof(T);
        
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

    public static bool Has<T>() where T : class => Container.IsRegistered<T>();
}