using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
///     Static lookup for the service container. The composition root (the "Services" autoload in
///     Scripts/Composition/CompositionRoot.cs) fills the container. This class registers nothing itself and names no
///     feature or composition type.
/// </summary>
public static class ServiceLocator
{
    private static IServiceContainer _container = new ServiceContainer();

    // Callbacks from Get<T>(callback), run by ExecutePendingCallbacks once their service is registered.
    private static readonly Dictionary<Type, List<Action<object>>> PendingCallbacks = new();

    public static IServiceContainer Container => _container;

    public static void ResetForTesting()
    {
        _container = new ServiceContainer();
        PendingCallbacks.Clear();
        SyncLogSink();
    }

    // The sink mirrors the ILog in the current container, the same source that Has and Get read.
    internal static void SyncLogSink()
    {
        ILog.Sink = _container.IsRegistered<ILog>() ? _container.Resolve<ILog>() : null;
    }

    public static void ExecutePendingCallbacks()
    {
        foreach (var keyValuePair in PendingCallbacks.ToList())
        {
            if (!_container.IsRegistered(keyValuePair.Key))
                continue;

            var service = _container.Resolve(keyValuePair.Key);
            foreach (var callback in keyValuePair.Value)
            {
                ILog.Print($"Running callback for {keyValuePair.Key.Name}");
                callback(service);
            }

            ILog.Print($"Removing callback for {keyValuePair.Key.Name}");
            PendingCallbacks.Remove(keyValuePair.Key);
        }
    }

    public static T Get<T>() where T : class
    {
        return Container.Resolve<T>();
    }

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
        if (!PendingCallbacks.TryGetValue(serviceType, out var value))
        {
            value = new List<Action<object>>();
            PendingCallbacks[serviceType] = value;
        }

        value.Add(obj => callback((T)obj));
    }

    public static bool Has<T>() where T : class
    {
        return Container.IsRegistered<T>();
    }
}
