using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
///     One container plus its pending-callback queue, and the resolver the
///     composition root injects into every <see cref="IDependent" /> node.
///     <see cref="Reset" /> keeps the host and swaps the container, so an
///     earlier resolver reads the new one. Names no feature or composition
///     type.
/// </summary>
public sealed class ServiceHost : IServiceResolver
{
    // Callbacks from Get<T>(callback), run once their service exists.
    private readonly Dictionary<Type, List<Action<object>>>
        _pendingCallbacks = new();

    public IServiceContainer Container { get; private set; } =
        new ServiceContainer();

    /// <summary>Replaces the container and drops pending callbacks.</summary>
    internal void Reset()
    {
        Container = new ServiceContainer();
        _pendingCallbacks.Clear();
    }

    public void ExecutePendingCallbacks()
    {
        var failures = new List<Exception>();
        foreach (var serviceType in _pendingCallbacks.Keys.ToList())
        {
            if (Container.IsRegistered(serviceType))
                RunCallbacksOf(serviceType, failures);
        }

        if (failures.Count == 1)
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new AggregateException(failures);
    }

    // The entry leaves the queue first, so a callback that throws or calls
    // back into the host can never make its type run twice.
    private void RunCallbacksOf(Type serviceType, List<Exception> failures)
    {
        if (!_pendingCallbacks.Remove(serviceType, out var callbacks))
            return;

        try
        {
            var service = Container.Resolve(serviceType);
            foreach (var callback in callbacks)
            {
                ILog.Print($"Running callback for {serviceType.Name}");
                var error = Invoke(callback, service);
                if (error != null)
                    Report(serviceType, error, failures);
            }
        }
        catch (Exception error)
        {
            Report(serviceType, error, failures);
        }
    }

    private static Exception? Invoke(Action<object> callback, object service)
    {
        try
        {
            callback(service);
            return null;
        }
        catch (Exception error)
        {
            return error;
        }
    }

    private static void Report(
        Type serviceType,
        Exception error,
        List<Exception> failures)
    {
        ILog.Error($"Callback for {serviceType.Name} failed: {error}");
        failures.Add(error);
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
