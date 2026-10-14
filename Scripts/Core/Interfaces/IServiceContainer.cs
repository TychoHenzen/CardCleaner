using System;
using System.Collections.Generic;

namespace CardCleaner.Scripts.Core.Interfaces;

public interface IServiceContainer
{
    void RegisterSingleton<T>(T instance) where T : class;
    void RegisterSingleton(Type serviceType, object instance);

    /// <summary>Builds TImplementation now through constructor injection and registers it as TInterface.</summary>
    void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface;

    T Resolve<T>() where T : class;
    object Resolve(Type type);
    bool IsRegistered<T>() where T : class;
    bool IsRegistered(Type serviceType);

    /// <summary>Builds a plain class through its widest public constructor, resolving each parameter from this
    ///     container; throws InvalidOperationException naming the class and the unregistered service.</summary>
    T Construct<T>() where T : class;

    /// <inheritdoc cref="Construct{T}" />
    object Construct(Type type);

    /// <summary>Lazily registers each concrete [Service] candidate under its [Service] types, or its interfaces when
    ///     it names none; never pass Godot objects. One counts as registered once all its constructor parameters
    ///     are, is built on its first Resolve and is then shared by all its types.</summary>
    void RegisterServiceClasses(IEnumerable<Type> candidates);
}
