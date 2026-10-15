using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
///     Owns one service container and its pending-callback queue, and is the <see cref="IServiceResolver" /> that the
///     composition root (the "Services" autoload in Scripts/Composition/CompositionRoot.cs) injects into every
///     <see cref="IDependent" /> node. It registers nothing itself and names no feature or composition type.
///     <see cref="Reset" /> replaces the container but keeps the host, so a resolver injected before a reset reads the
///     replacement container.
/// </summary>
public sealed class ServiceHost : IServiceResolver
{
    // Callbacks from Get<T>(callback), run by ExecutePendingCallbacks once their service is registered.
    private readonly Dictionary<Type, List<Action<object>>> _pendingCallbacks = new();

    public IServiceContainer Container { get; private set; } = new ServiceContainer();

    /// <summary>Replaces the container with an empty one and drops every pending callback.</summary>
    public void Reset()
    {
        Container = new ServiceContainer();
        _pendingCallbacks.Clear();
    }

    public void ExecutePendingCallbacks()
    {
        foreach (var keyValuePair in _pendingCallbacks.ToList())
        {
            if (!Container.IsRegistered(keyValuePair.Key))
                continue;

            var service = Container.Resolve(keyValuePair.Key);
            foreach (var callback in keyValuePair.Value)
            {
                ILog.Print($"Running callback for {keyValuePair.Key.Name}");
                callback(service);
            }

            ILog.Print($"Removing callback for {keyValuePair.Key.Name}");
            _pendingCallbacks.Remove(keyValuePair.Key);
        }
    }

    public T Get<T>() where T : class
    {
        return Container.Resolve<T>();
    }

    public void Get<T>(Action<T> callback) where T : class
    {
        var serviceType = typeof(T);

        // If service is already available, call callback immediately
        if (Container.IsRegistered<T>())
        {
            callback(Container.Resolve<T>());
            return;
        }

        // Otherwise, store callback for later execution
        if (!_pendingCallbacks.TryGetValue(serviceType, out var value))
        {
            value = new List<Action<object>>();
            _pendingCallbacks[serviceType] = value;
        }

        value.Add(obj => callback((T)obj));
    }

    public bool Has<T>() where T : class
    {
        return Container.IsRegistered<T>();
    }
}
