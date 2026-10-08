using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.DependencyInjection;

public class ServiceContainer : IServiceContainer
{
    private readonly Dictionary<Type, object> _singletons = new();

    public void RegisterSingleton<T>(T instance) where T : class
    {
        _singletons[typeof(T)] = instance;
    }
    public void RegisterSingleton(Type serviceType, object instance)
    {
        _singletons[serviceType] = instance;
    }

    public void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface, new()
    {
        var instance = new TImplementation();
        _singletons[typeof(TInterface)] = instance;
    }

    public T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    public object Resolve(Type type)
    {
        if (_singletons.TryGetValue(type, out var singleton))
            return singleton;

        throw new InvalidOperationException($"Service {type.Name} not registered");
    }

    public bool IsRegistered<T>() where T : class
    {
        return IsRegistered(typeof(T));
    }

    public bool IsRegistered(Type serviceType)
    {
        return _singletons.ContainsKey(serviceType);
    }
}