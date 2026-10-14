using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;

namespace CardCleaner.Scripts.Core.DependencyInjection;

/// <summary>
///     Finds the plain [Service] classes the container may build itself. Nodes and other Godot objects also carry
///     [Service], for AutoServiceProvider, which registers the live instance; the container must never build one, so
///     they are left out here.
/// </summary>
public static class ServiceClassScan
{
    /// <summary>
    ///     The non-abstract [Service] classes of the assembly that are not Godot objects and whose namespace is the
    ///     prefix or lies under it.
    /// </summary>
    public static IEnumerable<Type> PlainServiceClassesIn(Assembly assembly, string namespacePrefix)
    {
        return assembly.GetTypes().Where(type =>
            InNamespace(type.Namespace, namespacePrefix) &&
            type.IsClass && !type.IsAbstract &&
            !typeof(GodotObject).IsAssignableFrom(type) &&
            type.IsDefined(typeof(ServiceAttribute), false));
    }

    private static bool InNamespace(string? name, string prefix) =>
        name != null && (name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal));
}
