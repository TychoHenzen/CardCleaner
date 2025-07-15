using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.DependencyInjection;

public class ServiceContainer : IServiceContainer
{
    private readonly Dictionary<Type, Func<object>> _factories = new();
    private readonly Dictionary<Type, object> _singletons = new();
    private readonly Dictionary<Type, Type> _transients = new();

    public void RegisterSingleton<T>(T instance) where T : class
    {
        _singletons[typeof(T)] = instance;
    }
    public void RegisterSingleton(Type serviceType, object instance)
    {
        _singletons[serviceType] = instance;
    }

    public void RegisterTransient(Type serviceType, Type implementationType)
    {
        _transients[serviceType] = implementationType;
    }

    public void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface, new()
    {
        var instance = new TImplementation();
        _singletons[typeof(TInterface)] = instance;
    }

    public void RegisterTransient<TInterface, TImplementation>()
        where TImplementation : class, TInterface, new()
    {
        _transients[typeof(TInterface)] = typeof(TImplementation);
    }

    public void RegisterFactory<T>(Func<T> factory) where T : class
    {
        _factories[typeof(T)] = factory;
    }

    public T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    public object Resolve(Type type)
    {
        if (_factories.TryGetValue(type, out var factory))
            return factory();

        if (_singletons.TryGetValue(type, out var singleton))
            return singleton;

        if (!_transients.TryGetValue(type, out var implementationType))
            throw new InvalidOperationException($"Service {type.Name} not registered");

        var returned = Activator.CreateInstance(implementationType);
        if (returned == null)
            throw new InvalidOperationException($"Failed to create service {type.Name}");
        return returned;
    }

    public bool IsRegistered<T>() where T : class
    {
        return IsRegistered(typeof(T));
    }

    public bool IsRegistered(Type serviceType)
    {
        return _singletons.ContainsKey(serviceType) ||
               _transients.ContainsKey(serviceType) ||
               _factories.ContainsKey(serviceType);
    }
}