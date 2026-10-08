using System;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IServiceContainer
{
    void RegisterSingleton<T>(T instance) where T : class;
    void RegisterSingleton(Type serviceType, object instance);
    void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface, new();

    T Resolve<T>() where T : class;
    object Resolve(Type type);
    bool IsRegistered<T>() where T : class;
    bool IsRegistered(Type serviceType);
}