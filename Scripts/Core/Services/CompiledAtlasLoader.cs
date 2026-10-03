using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Services.CompiledAtlas;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Loads compiled tile atlas and mapping at runtime.
/// Builds a TileSet from the compiled atlas PNG for efficient rendering.
/// </summary>
public static class CompiledAtlasLoader
{
    private const string DefaultMappingPath = "res://Data/CompiledAtlas/atlas_mapping.json";

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

    private static AtlasMappingData? _cachedMapping;
    private static TileSet? _cachedTileSet;
    private static string? _cachedAtlasPath;

    /// <summary>
    /// Checks if a compiled atlas is available.
    /// </summary>
    public static bool IsCompiledAtlasAvailable(string? mappingPath = null)
    {
        mappingPath ??= DefaultMappingPath;
        var absolutePath = ProjectSettings.GlobalizePath(mappingPath);
        var exists = File.Exists(absolutePath);
        ILog.Print($"[CompiledAtlasLoader] IsCompiledAtlasAvailable: path={absolutePath}, exists={exists}");
        return exists;
    }

    /// <summary>
    /// Loads the atlas mapping from JSON.
    /// Returns null if file doesn't exist or is invalid.
    /// </summary>
    public static AtlasMappingData? LoadMapping(string? mappingPath = null)
    {
        ILog.Print("[CompiledAtlasLoader] LoadMapping called");
        mappingPath ??= DefaultMappingPath;

        // Return cached mapping if already loaded
        if (_cachedMapping != null && _cachedAtlasPath == mappingPath)
        {
            ILog.Print("[CompiledAtlasLoader] Returning cached mapping");
            return _cachedMapping;
        }

        var absolutePath = ProjectSettings.GlobalizePath(mappingPath);
        ILog.Print($"[CompiledAtlasLoader] LoadMapping absolutePath: {absolutePath}");

        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[CompiledAtlasLoader] Mapping file not found: {absolutePath}");
            return null;
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            ILog.Print($"[CompiledAtlasLoader] Read {json.Length} chars from mapping file");

            var loadedMapping = JsonSerializer.Deserialize<AtlasMappingData>(json, CreateJsonOptions());
            if (loadedMapping?.Atlas == null)
            {
                ILog.Print("[CompiledAtlasLoader] Mapping JSON has no atlas root");
                return null;
            }

            _cachedMapping = loadedMapping;
            _cachedAtlasPath = mappingPath;

            if (_cachedMapping != null)
            {
                ILog.Print($"[CompiledAtlasLoader] Loaded mapping: version={_cachedMapping.Version}, " +
                           $"atlas={((_cachedMapping.Atlas != null) ? "present" : "NULL")}, " +
                           $"sources={_cachedMapping.Sources?.Count ?? 0}");
            }
            else
            {
                ILog.Print("[CompiledAtlasLoader] JsonSerializer.Deserialize returned null");
            }

            return _cachedMapping;
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledAtlasLoader] Error loading mapping: {ex.Message}");
            ILog.Print($"[CompiledAtlasLoader] Stack: {ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Creates a TileSet from the compiled atlas.
    /// Returns null if atlas isn't available.
    /// </summary>
    public static TileSet? LoadCompiledTileSet(string? mappingPath = null)
    {
        ILog.Print("[CompiledAtlasLoader] LoadCompiledTileSet called");

        // Return cached TileSet if available
        if (_cachedTileSet != null && _cachedAtlasPath == (mappingPath ?? DefaultMappingPath))
        {
            ILog.Print("[CompiledAtlasLoader] Returning cached TileSet");
            return _cachedTileSet;
        }

        var mapping = LoadMapping(mappingPath);
        if (mapping?.Atlas == null)
        {
            ILog.Print($"[CompiledAtlasLoader] FAILED: mapping={mapping != null}, atlas={mapping?.Atlas != null}");
            return null;
        }

        ILog.Print(
            $"[CompiledAtlasLoader] Atlas info: path={mapping.Atlas.Path}, " +
            $"size={mapping.Atlas.Width}x{mapping.Atlas.Height}, " +
            $"tileSize={mapping.Atlas.TileSize}");

        try
        {
            var atlasTexture = LoadAtlasTexture(mapping.Atlas.Path);
            if (atlasTexture == null)
            {
                ILog.Print(
                    $"[CompiledAtlasLoader] FAILED: All load methods failed for: " +
                    mapping.Atlas.Path);
                return null;
            }

            var tileSet = CompiledAtlasTileSetBuilder.Build(mapping, atlasTexture, CreateJsonOptions());

            _cachedTileSet = tileSet;
            LogTileSetDetails(tileSet);

            return tileSet;
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledAtlasLoader] Error creating TileSet: {ex.Message}");
            return null;
        }
    }

    private static Texture2D? LoadAtlasTexture(string atlasPath)
    {
        var atlasTexture = TryLoadAtlasResource(atlasPath);
        return atlasTexture ?? LoadAtlasTextureFromFile(atlasPath);
    }

    private static Texture2D? TryLoadAtlasResource(string atlasPath)
    {
        var resourceExists = ResourceLoader.Exists(atlasPath);
        ILog.Print(
            $"[CompiledAtlasLoader] ResourceLoader.Exists({atlasPath})={resourceExists}");

        if (!resourceExists)
            return null;

        var atlasTexture = ResourceLoader.Load<Texture2D>(atlasPath);
        if (atlasTexture != null)
        {
            ILog.Print($"[CompiledAtlasLoader] Loaded atlas via ResourceLoader: {atlasPath}");
        }
        else
        {
            ILog.Print("[CompiledAtlasLoader] ResourceLoader.Load returned null");
        }

        return atlasTexture;
    }

    private static Texture2D? LoadAtlasTextureFromFile(string atlasPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
        ILog.Print($"[CompiledAtlasLoader] Trying direct file read: {absolutePath}");

        if (!File.Exists(absolutePath))
        {
            ILog.Print(
                $"[CompiledAtlasLoader] FAILED: Atlas PNG not found at: {absolutePath}");
            return null;
        }

        var image = Image.LoadFromFile(absolutePath);
        if (image == null)
        {
            ILog.Print("[CompiledAtlasLoader] FAILED: Image.LoadFromFile returned null");
            return null;
        }

        ILog.Print($"[CompiledAtlasLoader] Loaded image: {image.GetWidth()}x{image.GetHeight()}");
        var atlasTexture = ImageTexture.CreateFromImage(image);
        ILog.Print($"[CompiledAtlasLoader] Loaded atlas via direct file read: {atlasPath}");
        return atlasTexture;
    }

    private static void LogTileSetDetails(TileSet tileSet)
    {
        var sourceCount = tileSet.GetSourceCount();
        var source0 = tileSet.GetSource(0);
        var atlasSourceCheck = source0 as TileSetAtlasSource;
        var textureDims = atlasSourceCheck?.Texture?.GetSize() ?? Vector2.Zero;
        ILog.Print(
            $"[CompiledAtlasLoader] Created TileSet: {sourceCount} source(s), " +
            $"source 0 = {(source0 != null ? "present" : "MISSING")}, " +
            $"texture = {textureDims.X}x{textureDims.Y}");
    }

    /// <summary>
    /// Translates original tile coordinates to compiled atlas coordinates.
    /// Returns the original coordinates if no mapping exists.
    /// </summary>
    internal static AtlasCoordinateTranslation TranslateCoordinates(
        int originalSourceId,
        Vector2I originalAtlasCoords,
        AtlasMappingData? mapping = null)
    {
        mapping ??= _cachedMapping;

        if (mapping?.Sources == null)
            return new AtlasCoordinateTranslation(originalSourceId, originalAtlasCoords);

        var sourceKey = originalSourceId.ToString();
        var coordKey = $"{originalAtlasCoords.X},{originalAtlasCoords.Y}";

        if (mapping.Sources.TryGetValue(sourceKey, out var coordMappings) &&
            coordMappings.TryGetValue(coordKey, out var rect))
        {
            // When using compiled atlas, all tiles use sourceId 0
            return new AtlasCoordinateTranslation(0, new Vector2I(rect.X, rect.Y));
        }

        // No mapping found - return original (will fail at runtime if atlas-only mode)
        ILog.Print($"[CompiledAtlasLoader] No mapping for source {originalSourceId} coord {originalAtlasCoords}");
        return new AtlasCoordinateTranslation(originalSourceId, originalAtlasCoords);
    }

    /// <summary>
    /// Clears the cached mapping and tileset.
    /// Call this if the compiled atlas has been regenerated.
    /// </summary>
    public static void ClearCache()
    {
        _cachedMapping = null;
        _cachedTileSet = null;
        _cachedAtlasPath = null;
    }

    // JSON deserialization classes (matching compiler output)
    public class AtlasMappingData
    {
        [JsonPropertyName("version")] public string? Version { get; set; }
        [JsonPropertyName("atlas")] public AtlasInfo? Atlas { get; set; }
        [JsonPropertyName("sources")]
        public Dictionary<string, Dictionary<string, TileAtlasRect>>? Sources { get; set; }
    }

    public class AtlasInfo
    {
        [JsonPropertyName("path")] public string Path { get; set; } = "";
        [JsonPropertyName("width")] public int Width { get; set; }
        [JsonPropertyName("height")] public int Height { get; set; }
        [JsonPropertyName("tileSize")] public int TileSize { get; set; }
    }

    public class TileAtlasRect
    {
        [JsonPropertyName("x")] public int X { get; set; }
        [JsonPropertyName("y")] public int Y { get; set; }
        [JsonPropertyName("w")] public int W { get; set; }
        [JsonPropertyName("h")] public int H { get; set; }
    }

}
