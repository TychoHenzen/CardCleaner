using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Features.Worldgen.AutoTiling;

/// <summary>
/// Thread-safe registry for auto-tile format definitions.
/// Built-in formats are automatically registered on first access.
/// </summary>
public static class AutoTileFormatRegistry
{
    private static readonly object _lock = new();
    private static readonly Dictionary<string, AutoTileFormatDefinition> _formats = new(StringComparer.OrdinalIgnoreCase);
    private static bool _builtInsRegistered;

    /// <summary>
    /// Ensures built-in formats are registered. Called automatically on first access.
    /// Thread-safe.
    /// </summary>
    public static void EnsureBuiltInsRegistered()
    {
        if (_builtInsRegistered)
            return;

        lock (_lock)
        {
            if (_builtInsRegistered)
                return;

            RegisterInternal(BuiltInAutoTileFormats.CreateCorner16());
            RegisterInternal(BuiltInAutoTileFormats.CreateEdge16());
            RegisterInternal(BuiltInAutoTileFormats.CreateBlob47());

            _builtInsRegistered = true;
        }
    }

    /// <summary>
    /// Registers a custom auto-tile format. Built-in formats are registered automatically.
    /// </summary>
    /// <param name="format">The format definition to register.</param>
    /// <returns>True if registered successfully; false if a format with that name already exists.</returns>
    /// <exception cref="ArgumentNullException">If format is null.</exception>
    public static bool Register(AutoTileFormatDefinition format)
    {
        ArgumentNullException.ThrowIfNull(format);
        EnsureBuiltInsRegistered();

        lock (_lock)
        {
            if (_formats.ContainsKey(format.Name))
                return false;

            _formats[format.Name] = format;
            return true;
        }
    }

    /// <summary>
    /// Attempts to get a format by name (case-insensitive).
    /// </summary>
    /// <param name="name">The format name to look up.</param>
    /// <param name="format">The format if found; null otherwise.</param>
    /// <returns>True if the format was found.</returns>
    public static bool TryGet(string name, out AutoTileFormatDefinition? format)
    {
        EnsureBuiltInsRegistered();

        lock (_lock)
        {
            return _formats.TryGetValue(name, out format);
        }
    }

    /// <summary>
    /// Gets a format by name, throwing if not found.
    /// </summary>
    /// <param name="name">The format name to look up.</param>
    /// <returns>The format definition.</returns>
    /// <exception cref="KeyNotFoundException">If no format with that name exists.</exception>
    public static AutoTileFormatDefinition Get(string name)
    {
        if (TryGet(name, out var format) && format != null)
            return format;

        throw new KeyNotFoundException($"Auto-tile format '{name}' not found in registry.");
    }

    /// <summary>
    /// Gets all registered formats.
    /// </summary>
    /// <returns>A read-only collection of all format definitions.</returns>
    public static IReadOnlyCollection<AutoTileFormatDefinition> GetAll()
    {
        EnsureBuiltInsRegistered();

        lock (_lock)
        {
            return _formats.Values.ToArray();
        }
    }

    /// <summary>
    /// Checks if a format with the given name is registered.
    /// </summary>
    /// <param name="name">The format name to check.</param>
    /// <returns>True if a format with that name exists.</returns>
    public static bool Contains(string name)
    {
        EnsureBuiltInsRegistered();

        lock (_lock)
        {
            return _formats.ContainsKey(name);
        }
    }

    /// <summary>
    /// Unregisters a custom format. Built-in formats cannot be unregistered.
    /// </summary>
    /// <param name="name">The format name to unregister.</param>
    /// <returns>True if removed; false if not found or if it's a built-in format.</returns>
    public static bool Unregister(string name)
    {
        EnsureBuiltInsRegistered();

        lock (_lock)
        {
            if (!_formats.TryGetValue(name, out var format))
                return false;

            if (format.IsBuiltIn)
                return false;

            return _formats.Remove(name);
        }
    }

    /// <summary>
    /// Clears all custom formats from the registry. Built-in formats are preserved.
    /// Primarily used for testing.
    /// </summary>
    public static void ClearCustomFormats()
    {
        EnsureBuiltInsRegistered();

        lock (_lock)
        {
            var toRemove = new List<string>();
            foreach (var (name, format) in _formats)
            {
                if (!format.IsBuiltIn)
                    toRemove.Add(name);
            }

            foreach (var name in toRemove)
                _formats.Remove(name);
        }
    }

    /// <summary>
    /// Internal registration that bypasses built-in check (used during initialization).
    /// </summary>
    private static void RegisterInternal(AutoTileFormatDefinition format)
    {
        _formats[format.Name] = format;
    }
}
