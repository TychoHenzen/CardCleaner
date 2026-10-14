using System;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
///     The resolver the composition root injects into every <see cref="IDependent" /> node. It holds no container:
///     each call reads ServiceLocator at call time, because rebuilding the services replaces ServiceLocator.Container.
///     Deferred lookups go to ServiceLocator's single pending-callback queue.
/// </summary>
public sealed class LocatorServiceResolver : IServiceResolver
{
    public static readonly LocatorServiceResolver Instance = new();

    private LocatorServiceResolver()
    {
    }

    public T Get<T>() where T : class => ServiceLocator.Get<T>();

    public bool Has<T>() where T : class => ServiceLocator.Has<T>();

    public void Get<T>(Action<T> callback) where T : class => ServiceLocator.Get(callback);
}
