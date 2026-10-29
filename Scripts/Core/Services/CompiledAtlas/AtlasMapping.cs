using CardCleaner.Scripts.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace CardCleaner.Scripts.Core.Services.CompiledAtlas;

/// <summary>
/// The compiled atlas mapping: JSON data read from an absolute file path and the translation
/// of original tile coordinates to atlas coordinates. It resolves no <c>res://</c> path and loads no
/// engine resource; <see cref="CompiledAtlasLoader"/> resolves the path and owns the TileSet.
/// </summary>
public static class AtlasMapping
{
    /// <summary>
    /// Loads the mapping from an absolute file path. Every call reads the file again.
    /// Returns null if the file is missing or invalid.
    /// </summary>
    public static AtlasMappingData? Load(string absolutePath)
    {
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[AtlasMapping] Mapping file not found: {absolutePath}");
            return null;
        }

        var loaded = ReadMapping(absolutePath);
        if (loaded == null)
            return null;

        ILog.Print($"[AtlasMapping] Loaded mapping: version={loaded.Version}, sources={loaded.Sources?.Count ?? 0}");
        return loaded;
    }

    /// <summary>
    /// Translates original tile coordinates to compiled atlas coordinates using the given mapping.
    /// The original coordinates come back when there is no mapping or no entry for them.
    /// </summary>
    internal static AtlasCoordinateTranslation Translate(
        int originalSourceId,
        Vector2I originalAtlasCoords,
        AtlasMappingData? mapping)
    {
        var original = new AtlasCoordinateTranslation(originalSourceId, originalAtlasCoords);
        if (mapping?.Sources == null)
            return original;

        var coordKey = $"{originalAtlasCoords.X},{originalAtlasCoords.Y}";
        if (mapping.Sources.TryGetValue(originalSourceId.ToString(), out var coordMappings) &&
            coordMappings.TryGetValue(coordKey, out var rect))
        {
            // When using compiled atlas, all tiles use sourceId 0
            return new AtlasCoordinateTranslation(0, new Vector2I(rect.X, rect.Y));
        }

        ILog.Print($"[AtlasMapping] No mapping for source {originalSourceId} coord {originalAtlasCoords}");
        return original;
    }

    private static AtlasMappingData? ReadMapping(string absolutePath)
    {
        try
        {
            var json = File.ReadAllText(absolutePath);
            var loaded = JsonSerializer.Deserialize<AtlasMappingData>(json, CreateJsonOptions());
            if (loaded?.Atlas != null)
                return loaded;

            ILog.Print("[AtlasMapping] Mapping JSON has no atlas root");
            return null;
        }
        catch (Exception ex)
        {
            ILog.Print($"[AtlasMapping] Error loading mapping: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates fresh JsonSerializerOptions per call to avoid assembly unload issues.
    /// See: https://github.com/godotengine/godot/issues/78513
    /// </summary>
    private static JsonSerializerOptions CreateJsonOptions() => new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // JSON deserialization classes (matching compiler output)
    public class AtlasMappingData
    {
        [JsonPropertyName("version")] public string? Version { get; init; }
        [JsonPropertyName("atlas")] public AtlasInfo? Atlas { get; init; }
        [JsonPropertyName("sources")]
        public Dictionary<string, Dictionary<string, TileAtlasRect>>? Sources { get; init; }
    }

    public class AtlasInfo
    {
        [JsonPropertyName("path")] public string Path { get; init; } = "";
        [JsonPropertyName("width")] public int Width { get; init; }
        [JsonPropertyName("height")] public int Height { get; init; }
        [JsonPropertyName("tileSize")] public int TileSize { get; init; }
    }

    public class TileAtlasRect
    {
        [JsonPropertyName("x")] public int X { get; init; }
        [JsonPropertyName("y")] public int Y { get; init; }
        [JsonPropertyName("w")] public int W { get; init; }
        [JsonPropertyName("h")] public int H { get; init; }
    }
}
