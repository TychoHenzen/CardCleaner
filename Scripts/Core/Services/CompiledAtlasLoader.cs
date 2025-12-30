using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
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

    private static readonly JsonSerializerOptions JsonOptions = new()
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
        return File.Exists(absolutePath);
    }

    /// <summary>
    /// Loads the atlas mapping from JSON.
    /// Returns null if file doesn't exist or is invalid.
    /// </summary>
    public static AtlasMappingData? LoadMapping(string? mappingPath = null)
    {
        mappingPath ??= DefaultMappingPath;

        // Return cached mapping if already loaded
        if (_cachedMapping != null && _cachedAtlasPath == mappingPath)
            return _cachedMapping;

        var absolutePath = ProjectSettings.GlobalizePath(mappingPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[CompiledAtlasLoader] Mapping file not found: {mappingPath}");
            return null;
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            _cachedMapping = JsonSerializer.Deserialize<AtlasMappingData>(json, JsonOptions);
            _cachedAtlasPath = mappingPath;

            if (_cachedMapping != null)
                ILog.Print($"[CompiledAtlasLoader] Loaded mapping with {_cachedMapping.Sources?.Count ?? 0} sources");

            return _cachedMapping;
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledAtlasLoader] Error loading mapping: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Creates a TileSet from the compiled atlas.
    /// Returns null if atlas isn't available.
    /// </summary>
    public static TileSet? LoadCompiledTileSet(string? mappingPath = null)
    {
        // Return cached TileSet if available
        if (_cachedTileSet != null && _cachedAtlasPath == (mappingPath ?? DefaultMappingPath))
            return _cachedTileSet;

        var mapping = LoadMapping(mappingPath);
        if (mapping?.Atlas == null)
            return null;

        try
        {
            var atlasPath = mapping.Atlas.Path;
            Texture2D? atlasTexture = null;

            // Try loading via ResourceLoader first (works when properly imported)
            if (ResourceLoader.Exists(atlasPath))
            {
                atlasTexture = ResourceLoader.Load<Texture2D>(atlasPath);
                if (atlasTexture != null)
                    ILog.Print($"[CompiledAtlasLoader] Loaded atlas via ResourceLoader: {atlasPath}");
            }

            // Fallback: load PNG directly from disk and create ImageTexture
            if (atlasTexture == null)
            {
                var absolutePath = ProjectSettings.GlobalizePath(atlasPath);
                if (!File.Exists(absolutePath))
                {
                    ILog.Print($"[CompiledAtlasLoader] Atlas PNG not found: {absolutePath}");
                    return null;
                }

                var image = Image.LoadFromFile(absolutePath);
                if (image == null)
                {
                    ILog.Print($"[CompiledAtlasLoader] Failed to load image from: {absolutePath}");
                    return null;
                }

                atlasTexture = ImageTexture.CreateFromImage(image);
                ILog.Print($"[CompiledAtlasLoader] Loaded atlas via direct file read: {atlasPath}");
            }

            if (atlasTexture == null)
            {
                ILog.Print($"[CompiledAtlasLoader] All load methods failed for: {atlasPath}");
                return null;
            }

            // Create TileSet with matching tile size
            var tileSet = new TileSet();
            tileSet.TileSize = new Vector2I(mapping.Atlas.TileSize, mapping.Atlas.TileSize);

            // Create a single TileSetAtlasSource for the compiled atlas
            var atlasSource = new TileSetAtlasSource();
            atlasSource.Texture = atlasTexture;
            atlasSource.TextureRegionSize = tileSet.TileSize;
            atlasSource.UseTexturePadding = false;

            // Create tiles for all mapped coordinates
            if (mapping.Sources != null)
            {
                foreach (var (sourceIdStr, coordMappings) in mapping.Sources)
                {
                    foreach (var (coordKey, rect) in coordMappings)
                    {
                        // The atlas coords in the mapping are where this tile lives in the compiled atlas
                        var atlasCoords = new Vector2I(rect.X, rect.Y);
                        var tileSize = new Vector2I(rect.W, rect.H);

                        // Create the tile at these atlas coordinates if not already exists
                        if (!atlasSource.HasTile(atlasCoords))
                        {
                            atlasSource.CreateTile(atlasCoords, tileSize);
                        }
                    }
                }
            }

            // Add the atlas source to the tileset at index 0
            // All tiles will use sourceId 0 when using compiled atlas
            tileSet.AddSource(atlasSource, 0);

            _cachedTileSet = tileSet;
            ILog.Print($"[CompiledAtlasLoader] Created TileSet from compiled atlas with 1 source");

            return tileSet;
        }
        catch (Exception ex)
        {
            ILog.Print($"[CompiledAtlasLoader] Error creating TileSet: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Translates original tile coordinates to compiled atlas coordinates.
    /// Returns the original coordinates if no mapping exists.
    /// </summary>
    public static (int sourceId, Vector2I atlasCoords) TranslateCoordinates(
        int originalSourceId,
        Vector2I originalAtlasCoords,
        AtlasMappingData? mapping = null)
    {
        mapping ??= _cachedMapping;

        if (mapping?.Sources == null)
            return (originalSourceId, originalAtlasCoords);

        var sourceKey = originalSourceId.ToString();
        var coordKey = $"{originalAtlasCoords.X},{originalAtlasCoords.Y}";

        if (mapping.Sources.TryGetValue(sourceKey, out var coordMappings) &&
            coordMappings.TryGetValue(coordKey, out var rect))
        {
            // When using compiled atlas, all tiles use sourceId 0
            return (0, new Vector2I(rect.X, rect.Y));
        }

        // No mapping found - return original (will fail at runtime if atlas-only mode)
        ILog.Print($"[CompiledAtlasLoader] No mapping for source {originalSourceId} coord {originalAtlasCoords}");
        return (originalSourceId, originalAtlasCoords);
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
        [JsonPropertyName("sources")] public Dictionary<string, Dictionary<string, TileAtlasRect>>? Sources { get; set; }
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
