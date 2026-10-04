using System;
using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.AutoTiling.Transitions;

/// <summary>
/// Reads the compiled transition_map.json from disk, falling back to an empty map when it is missing or invalid.
/// </summary>
internal static class TransitionMapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    internal static CompiledTransitionMap Load(string path)
    {
        try
        {
            var absolutePath = ProjectSettings.GlobalizePath(path);
            if (!File.Exists(absolutePath))
            {
                ILog.Print($"[CompiledTransitionResolver] Transition map not found: {absolutePath}");
                return new CompiledTransitionMap();
            }

            var json = File.ReadAllText(absolutePath);
            var map = JsonSerializer.Deserialize<CompiledTransitionMap>(json, JsonOptions);

            if (map == null)
            {
                ILog.Print("[CompiledTransitionResolver] Failed to deserialize transition map");
                return new CompiledTransitionMap();
            }

            ILog.Print($"[CompiledTransitionResolver] Loaded {map.Transitions.Count} transitions from {path}");
            return map;
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledTransitionResolver] Error loading transition map: {ex.Message}");
            return new CompiledTransitionMap();
        }
    }
}
