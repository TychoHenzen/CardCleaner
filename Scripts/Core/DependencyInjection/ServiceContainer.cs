using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.DependencyInjection;

public class ServiceContainer : IServiceContainer
{
    private readonly Dictionary<Type, object> _singletons = new();
    private readonly Dictionary<Type, Type> _transients = new();
    private readonly Dictionary<Type, Func<object>> _factories = new();

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
        _factories[typeof(T)] = factory;
    }

    public T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    public object Resolve(Type type)
    {
        // Try singletons first
        if (_singletons.TryGetValue(type, out var singleton))
            return singleton;

        // Try factories
        if (_factories.TryGetValue(type, out var factory))
            return factory();

        // Try transients
        if (_transients.TryGetValue(type, out var implementationType))
            return Activator.CreateInstance(implementationType);

        throw new InvalidOperationException($"Service {type.Name} not registered");
    }


    public bool IsRegistered<T>() where T : class
    {
        return IsRegistered(typeof(T));
    }
    
    public bool IsRegistered(Type type)
    {
        return _singletons.ContainsKey(type) ||
               _transients.ContainsKey(type) ||
               _factories.ContainsKey(type);
    }
}