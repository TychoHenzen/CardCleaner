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

    // Scanned [Service] classes and RegisterLazy factories, built on first Resolve. One entry is shared by every
    // service type of its class or factory.
    private readonly Dictionary<Type, LazyService> _lazy = new();

    // The registrations SealAppScope recorded; null until it runs. DropSceneScope returns the maps to these.
    private Dictionary<Type, object>? _appSingletons;
    private Dictionary<Type, LazyService>? _appLazy;

    public void RegisterSingleton<T>(T instance) where T : class
    {
        RegisterSingleton(typeof(T), instance);
    }

    public void RegisterSingleton(Type serviceType, object instance)
    {
        WarnIfAppInstanceWasHandedOut(serviceType, instance);
        _singletons[serviceType] = instance;
        _lazy.Remove(serviceType);
    }

    public void SealAppScope()
    {
        _appSingletons = new Dictionary<Type, object>(_singletons);
        _appLazy = new Dictionary<Type, LazyService>(_lazy);
    }

    public void DropSceneScope()
    {
        if (_appSingletons == null || _appLazy == null)
            return;

        _singletons.Clear();
        foreach (var (type, instance) in _appSingletons)
            _singletons[type] = instance;
        _lazy.Clear();
        foreach (var (type, entry) in _appLazy)
            _lazy[type] = entry;
        // A class built from a scene service holds that scene's objects, so it is built again in the next scene.
        foreach (var entry in _appLazy.Values.Where(entry => entry.SceneBound))
        {
            entry.Instance = null;
            entry.SceneBound = false;
        }
    }

    public void RegisterSingleton<TInterface, TImplementation>()
        where TImplementation : class, TInterface
    {
        RegisterSingleton(typeof(TInterface), Construct(typeof(TImplementation)));
    }

    public void RegisterLazy<T>(Func<T> factory, params Type[] alsoAs) where T : class
    {
        var serviceTypes = alsoAs.Prepend(typeof(T)).ToArray();
        var entry = new LazyService(typeof(T)) { Factory = factory, ServiceTypes = serviceTypes };
        foreach (var serviceType in serviceTypes)
        {
            _singletons.Remove(serviceType);
            _lazy[serviceType] = entry;
        }
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
            return lazy.Instance ?? Build(lazy);

        throw new InvalidOperationException($"Service {type.Name} not registered");
    }

    // Builds a lazy entry once. A scanned class is scene-bound when any constructor argument came from the scene
    // scope; a factory takes no arguments from the container, so it never is.
    private object Build(LazyService lazy)
    {
        if (lazy.Factory != null)
            return lazy.Instance = BuildFromFactory(lazy);

        var parameters = GreediestConstructor(lazy.Implementation).GetParameters();
        lazy.Instance = Construct(lazy.Implementation);
        lazy.SceneBound = parameters.Any(parameter => IsSceneScoped(parameter.ParameterType));
        return lazy.Instance;
    }

    // The compiler checks only the factory's own type, so a type passed in alsoAs is checked here, once, on the built
    // instance. Without it the wrong type surfaced as an InvalidCastException in whichever Resolve<T> asked for it.
    private static object BuildFromFactory(LazyService lazy)
    {
        var instance = lazy.Factory!();
        var unmet = lazy.ServiceTypes.FirstOrDefault(serviceType => !serviceType.IsInstanceOfType(instance));
        if (unmet != null)
            throw new InvalidOperationException(
                $"The lazy {lazy.Implementation.Name} factory built {instance?.GetType().Name ?? "null"}, " +
                $"which is not a {unmet.Name}");
        return instance!;
    }

    // Scene scope is whatever was registered or built from a scene registration after SealAppScope.
    private bool IsSceneScoped(Type type)
    {
        if (_appSingletons == null || _appLazy == null)
            return false;
        if (_singletons.TryGetValue(type, out var instance))
            return !(_appSingletons.TryGetValue(type, out var appInstance) && ReferenceEquals(appInstance, instance));
        return _lazy.TryGetValue(type, out var lazy) && (lazy.SceneBound || !_appLazy.ContainsKey(type));
    }

    // A scene registration replaces an app-scope service only for later lookups; a node that already resolved the
    // app one keeps it, which is almost always a wiring mistake.
    private void WarnIfAppInstanceWasHandedOut(Type serviceType, object instance)
    {
        if (_appLazy == null || !_appLazy.TryGetValue(serviceType, out var appEntry))
            return;
        if (appEntry.Instance != null && !appEntry.SceneBound && !ReferenceEquals(appEntry.Instance, instance))
            ILog.Warning($"{serviceType.Name} was already resolved as {appEntry.Implementation.Name} from the " +
                $"app scope; registering {instance.GetType().Name} affects only later lookups");
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
        if (lazy.Instance != null || lazy.Factory != null)
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

        // Set for a RegisterLazy entry, which builds through it instead of a constructor.
        public Func<object>? Factory { get; init; }

        // Every type a RegisterLazy entry is registered under; the built instance must be each of them.
        public Type[] ServiceTypes { get; init; } = [];

        // True when Instance was built from a scene-scope service; DropSceneScope then clears Instance.
        public bool SceneBound { get; set; }
    }
}
