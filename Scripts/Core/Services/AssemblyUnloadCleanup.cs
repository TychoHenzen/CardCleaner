using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Handles cleanup of static caches before assembly unload to prevent Godot issue #78513.
/// See: https://github.com/godotengine/godot/issues/78513
///
/// The problem: System.Text.Json maintains internal caches that hold references to types
/// from loaded assemblies. When Godot tries to unload the C# assembly for hot reload,
/// these cached references prevent proper unloading, breaking all C# scripts.
///
/// The solution: Use a ModuleInitializer to register cleanup handlers that clear these
/// caches before the assembly unloads.
/// </summary>
internal static class AssemblyUnloadCleanup
{
    private static readonly ClearList ProjectClears = new();
    private static bool _initialized;

    /// <summary>
    /// Registers a cache clear to run when the assembly unloads. Each cache owner registers its own
    /// from a static constructor; this works whether or not <see cref="Initialize"/> has run.
    /// </summary>
    internal static void Register(Action clear) => ProjectClears.Register(clear);

    /// <summary>
    /// Called automatically when the assembly is loaded.
    /// Registers cleanup handlers for assembly unloading.
    /// </summary>
    [ModuleInitializer]
    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // Get the current assembly's load context
        var context = AssemblyLoadContext.GetLoadContext(typeof(AssemblyUnloadCleanup).Assembly);
        if (context != null && context.IsCollectible)
        {
            context.Unloading += OnUnloading;
        }
    }

    /// <summary>
    /// Called when the assembly is about to be unloaded.
    /// Clears all known static caches that could prevent unloading.
    /// </summary>
    private static void OnUnloading(AssemblyLoadContext context)
    {
        ClearSystemTextJsonCache();
        ProjectClears.RunAll();
    }

    /// <summary>
    /// Clears System.Text.Json's internal type cache.
    /// Uses reflection to access the internal JsonSerializerOptionsUpdateHandler.ClearCache method.
    /// </summary>
    private static void ClearSystemTextJsonCache()
    {
        try
        {
            var assembly = typeof(JsonSerializerOptions).Assembly;
            var updateHandlerType = assembly.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");

            if (updateHandlerType == null)
            {
                // Try alternative name used in some .NET versions
                updateHandlerType = assembly.GetType(
                    "System.Text.Json.Serialization.Metadata.JsonSerializerOptionsUpdateHandler");
            }

            var clearCacheMethod = updateHandlerType?.GetMethod(
                "ClearCache",
                BindingFlags.Static | BindingFlags.Public);
            clearCacheMethod?.Invoke(null, new object?[] { null });
        }
        catch (Exception)
        {
            // Silently ignore - cache clearing is best-effort
            // If it fails, the worst case is assembly unload still fails (same as before)
        }
    }

    /// <summary>
    /// The registered clears of project-specific static caches that could hold type references. An instance, so
    /// tests check its rules on a list of their own and never clear the game's caches.
    /// </summary>
    internal sealed class ClearList
    {
        private readonly List<Action> _clears = new();
        private readonly object _lock = new();

        internal void Register(Action clear)
        {
            lock (_lock)
            {
                _clears.Add(clear);
            }
        }

        /// <summary>Runs every registered clear; one that throws does not stop the others.</summary>
        internal void RunAll()
        {
            Action[] snapshot;
            lock (_lock)
            {
                snapshot = _clears.ToArray();
            }

            foreach (var clear in snapshot)
            {
                try
                {
                    clear();
                }
                catch (Exception)
                {
                    // Silently ignore - one failing cache must not stop the others
                }
            }
        }
    }

    /// <summary>
    /// Creates a new JsonSerializerOptions instance with standard settings.
    /// Use this instead of static JsonSerializerOptions to avoid caching issues.
    /// </summary>
    public static JsonSerializerOptions CreateJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
    }
}
