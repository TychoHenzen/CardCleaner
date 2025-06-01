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
        // Fix: Properly cast the factory function to return object
        _factories[typeof(T)] = factory;
    }

    public T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    public object Resolve(Type type)
    {
        // Fix: Check factories first to allow them to override singletons
        // This matches the test expectation in TestFactoryOverridesSingleton
        if (_factories.TryGetValue(type, out var factory))
            return factory();

        // Try singletons second
        if (_singletons.TryGetValue(type, out var singleton))
            return singleton;

        // Try transients third
        if (!_transients.TryGetValue(type, out var implementationType))
            throw new InvalidOperationException($"Service {type.Name} not registered");
        
        var returned = Activator.CreateInstance(implementationType);
        if(returned == null)
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