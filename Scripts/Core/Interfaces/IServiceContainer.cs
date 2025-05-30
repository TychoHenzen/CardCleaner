using System;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IServiceContainer
{
    void RegisterSingleton<T>(T instance) where T : class;
    void RegisterSingleton<TInterface, TImplementation>() 
        where TImplementation : class, TInterface, new();
    void RegisterTransient<TInterface, TImplementation>() 
        where TImplementation : class, TInterface, new();
    void RegisterFactory<T>(Func<T> factory) where T : class;
        
    T Resolve<T>() where T : class;
    object Resolve(Type type);
    bool IsRegistered<T>() where T : class;
    bool IsRegistered(Type serviceType);
}