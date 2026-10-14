using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CardCleaner.Scripts.Core.Interfaces;
using IServiceProvider = CardCleaner.Scripts.Core.Interfaces.IServiceProvider;

namespace CardCleaner.Scripts.Core.DependencyInjection;

public class ServiceContainer : IServiceContainer
{
    private readonly Dictionary<Type, object> _singletons = new();

    // Scanned [Service] classes, built on first Resolve. One entry is shared by every service type of its class.
    private readonly Dictionary<Type, LazyService> _lazy = new();

    public void RegisterSingleton<T>(T instance) where T : class
    {
        RegisterSingleton(typeof(T), instance);
    }

    public void RegisterSingleton(Type serviceType, object instance)
    {
        _singletons[serviceType] = instance;
        _lazy.Remove(serviceType);
    }

    public void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface
    {
        RegisterSingleton(typeof(TInterface), Construct(typeof(TImplementation)));
    }

    public T Resolve<T>() where T : class
    {
        return (T)Resolve(typeof(T));
    }

    public object Resolve(Type type)
    {
        if (_singletons.TryGetValue(type, out var singleton))
            return singleton;

        if (_lazy.TryGetValue(type, out var lazy))
            return lazy.Instance ??= Construct(lazy.Implementation);

        throw new InvalidOperationException($"Service {type.Name} not registered");
    }

    public bool IsRegistered<T>() where T : class
    {
        return IsRegistered(typeof(T));
    }

    public bool IsRegistered(Type serviceType)
    {
        return IsRegistered(serviceType, new HashSet<Type>());
    }

    public T Construct<T>() where T : class
    {
        return (T)Construct(typeof(T));
    }

    public object Construct(Type type)
    {
        var constructor = GreediestConstructor(type);
        var arguments = constructor.GetParameters()
            .Select(parameter => ResolveDependency(type, parameter.ParameterType))
            .ToArray();
        return constructor.Invoke(arguments);
    }

    public void RegisterServiceClasses(IEnumerable<Type> candidates)
    {
        foreach (var type in candidates.Where(IsPlainServiceClass))
        {
            var entry = new LazyService(type);
            foreach (var serviceType in ServiceTypesOf(type))
                // ASSUMPTION: a scan never replaces an explicit registration of the same type. Compose scans first,
                // so this only matters for a scan that runs later; the brief fixes only the opposite order.
                if (!_singletons.ContainsKey(serviceType))
                    _lazy[serviceType] = entry;
        }
    }

    private bool IsRegistered(Type serviceType, HashSet<Type> visiting)
    {
        if (_singletons.ContainsKey(serviceType))
            return true;
        if (!_lazy.TryGetValue(serviceType, out var lazy))
            return false;
        if (lazy.Instance != null)
            return true;

        // A class already on the path is a dependency cycle, which can never be built.
        if (!visiting.Add(lazy.Implementation))
            return false;
        var registered = GreediestConstructorOrNull(lazy.Implementation)?.GetParameters()
            .All(parameter => IsRegistered(parameter.ParameterType, visiting)) ?? false;
        visiting.Remove(lazy.Implementation);
        return registered;
    }

    private object ResolveDependency(Type consumer, Type dependency)
    {
        if (!IsRegistered(dependency))
            throw new InvalidOperationException(
                $"Cannot construct {consumer.Name}: service {dependency.Name} not registered");
        return Resolve(dependency);
    }

    private static ConstructorInfo GreediestConstructor(Type type)
    {
        return GreediestConstructorOrNull(type) ??
            throw new InvalidOperationException($"Cannot construct {type.Name}: it has no public constructor");
    }

    // ASSUMPTION: two public constructors with the same, highest parameter count are rare; the first one reflection
    // reports wins. No requirement names a tie-break.
    private static ConstructorInfo? GreediestConstructorOrNull(Type type)
    {
        return type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(constructor => constructor.GetParameters().Length)
            .FirstOrDefault();
    }

    // Godot objects are filtered out by the caller (ServiceClassScan): naming a Godot type here would put the container
    // in the dependency gate's UI layer.
    private static bool IsPlainServiceClass(Type type)
    {
        return type.IsClass && !type.IsAbstract && !type.ContainsGenericParameters &&
            type.IsDefined(typeof(ServiceAttribute), false);
    }

    // The same rule as AutoServiceProvider: the attribute's types, or the class's own interfaces when it names none.
    private static IEnumerable<Type> ServiceTypesOf(Type type)
    {
        var attribute = type.GetCustomAttribute<ServiceAttribute>(false)!;
        return attribute.ServiceTypes.Length > 0
            ? attribute.ServiceTypes
            : type.GetInterfaces().Where(i => i != typeof(IServiceProvider) && !i.Name.StartsWith("Godot"));
    }

    private sealed class LazyService(Type implementation)
    {
        public Type Implementation { get; } = implementation;
        public object? Instance { get; set; }
    }
}
