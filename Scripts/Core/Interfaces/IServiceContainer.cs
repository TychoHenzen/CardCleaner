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

    /// <summary>
    ///     Builds a plain class through its public constructor with the most parameters, resolving each parameter from
    ///     this container. Throws InvalidOperationException naming the class and the missing service when a parameter
    ///     is not registered.
    /// </summary>
    T Construct<T>() where T : class;

    /// <inheritdoc cref="Construct{T}" />
    object Construct(Type type);

    /// <summary>
    ///     Registers each candidate that is a concrete [Service] class, lazily, under its [Service] types (or its
    ///     interfaces when it names none). The caller passes plain classes only, never Godot objects. An entry counts as registered once every parameter
    ///     of its constructor is registered; it is built on its first Resolve and then reused under all its types.
    /// </summary>
    void RegisterServiceClasses(IEnumerable<Type> candidates);
}
