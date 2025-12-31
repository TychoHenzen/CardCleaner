using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;
using VariationMode = CardCleaner.Scripts.Features.Deckbuilder.Tiles.VariationMode;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Result of loading tile registry data
/// </summary>
public record TileRegistryResult(string TilesetPath, List<TileDefinition> Tiles);

/// <summary>
/// Loads tile definitions from JSON data files
/// </summary>
public static class TileDataLoader
{
    private const string DefaultTilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

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

    /// <summary>
    /// Load tiles and tileset path from JSON
    /// </summary>
    public static TileRegistryResult LoadTileRegistry(string? path = null)
    {
        path ??= DefaultTilesPath;

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TileDataLoader] File not found: {absolutePath}");
            return new TileRegistryResult(DefaultTilesetPath, []);
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, CreateJsonOptions());
            if (data?.Tiles == null)
            {
                ILog.Print("[TileDataLoader] Invalid JSON structure");
                return new TileRegistryResult(DefaultTilesetPath, []);
            }

            var tilesetPath = data.Tileset ?? DefaultTilesetPath;
            var tiles = new List<TileDefinition>();
            var index = 0;
            foreach (var tileData in data.Tiles)
            {
                var tile = ConvertToTileDefinition(tileData, index);
                if (tile != null)
                    tiles.Add(tile);
                index++;
            }

            ILog.Print($"[TileDataLoader] Loaded {tiles.Count} tiles from {path} using tileset {tilesetPath}");
            return new TileRegistryResult(tilesetPath, tiles);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TileDataLoader] Error loading tiles: {ex.Message}");
            return new TileRegistryResult(DefaultTilesetPath, []);
        }
    }

    /// <summary>
    /// Load biome definitions from JSON
    /// </summary>
    public static Dictionary<string, BiomeData> LoadBiomes(string? path = null)
    {
        path ??= DefaultTilesPath;

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TileDataLoader] File not found for biomes: {absolutePath}");
            return new Dictionary<string, BiomeData>();
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, CreateJsonOptions());
            if (data?.Biomes == null || data.Biomes.Count == 0)
            {
                ILog.Print("[TileDataLoader] No biomes section found in JSON");
                return new Dictionary<string, BiomeData>();
            }

            ILog.Print($"[TileDataLoader] Loaded {data.Biomes.Count} biomes from {path}");
            return data.Biomes;
        }
        catch (Exception ex)
        {
            ILog.Print($"[TileDataLoader] Error loading biomes: {ex.Message}");
            return new Dictionary<string, BiomeData>();
        }
    }

    private static TileDefinition? ConvertToTileDefinition(TileData data, int fileIndex)
    {
        if (string.IsNullOrEmpty(data.Id) || string.IsNullOrEmpty(data.Name))
            return null;

        var passability = ParsePassability(data.Passability);
        var layer = ParseLayer(data.Layer);
        var biomes = ParseBiomes(data.Biomes);
        var atlasCoords = new Vector2I(data.AtlasCoords?.X ?? 0, data.AtlasCoords?.Y ?? 0);
        var size = data.Size != null ? new Vector2I(data.Size.X, data.Size.Y) : (Vector2I?)null;
        var autoTileFormat = ParseAutoTileFormat(data.AutoTileFormat);
        var variations = ParseVariations(data.Variations);
        var variationMode = ParseVariationMode(data.VariationMode);
        var animation = ParseAnimation(data.Animation);
        // Use explicit dominance if provided, otherwise default to file order index
        var dominance = data.Dominance ?? fileIndex;

        return new TileDefinition(
            id: data.Id,
            name: data.Name,
            passability: passability,
            atlasCoords: atlasCoords,
            sourceId: data.SourceId ?? 4,
            layer: layer,
            elevation: data.Elevation ?? 0f,
            isTransparent: data.IsTransparent,
            allowedBiomes: biomes,
            size: size,
            decorationDensity: data.DecorationDensity ?? 1.0f,
            autoTileVariants: ParseAutoTileVariants(data.AutoTileVariants, autoTileFormat),
            autoTileFormat: autoTileFormat,
            variations: variations,
            variationMode: variationMode,
            animation: animation,
            dominance: dominance,
            innerTerrainId: data.InnerTerrain,
            outerTerrainId: data.OuterTerrain);
    }

    private static AutoTileFormat ParseAutoTileFormat(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "blob47" => AutoTileFormat.Blob47,
            "edge16" => AutoTileFormat.Edge16,
            _ => AutoTileFormat.Corner16
        };
    }

    private static VariationMode ParseVariationMode(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "pergeneration" or "per_generation" => VariationMode.PerGeneration,
            "contextual" => VariationMode.Contextual,
            _ => VariationMode.PerInstance
        };
    }

    private static Vector2I[]? ParseVariations(Vector2IData[]? variations)
    {
        if (variations == null || variations.Length == 0)
            return null;

        var result = new Vector2I[variations.Length];
        for (var i = 0; i < variations.Length; i++)
        {
            result[i] = new Vector2I(variations[i].X, variations[i].Y);
        }

        return result;
    }

    private static TileAnimation? ParseAnimation(AnimationData? data)
    {
        if (data?.Frames == null || data.Frames.Length == 0)
            return null;

        var frames = new Vector2I[data.Frames.Length];
        for (var i = 0; i < data.Frames.Length; i++)
        {
            frames[i] = new Vector2I(data.Frames[i].X, data.Frames[i].Y);
        }

        return new TileAnimation(frames, data.FrameDuration);
    }

    private static TilePassability ParsePassability(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "passable" => TilePassability.Passable,
            "solid" => TilePassability.Solid,
            "partially_passable" => TilePassability.PartiallyPassable,
            _ => TilePassability.Passable
        };
    }

    private static TileLayer ParseLayer(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "terrain" => TileLayer.Terrain,
            "decoration" => TileLayer.Decoration,
            "structure" => TileLayer.Structure,
            "effects" => TileLayer.Effects,
            _ => TileLayer.Terrain
        };
    }

    private static HashSet<string>? ParseBiomes(List<string>? biomes)
    {
        if (biomes == null || biomes.Count == 0)
            return null;

        var result = new HashSet<string>();
        foreach (var biome in biomes)
        {
            var normalized = biome.ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(normalized))
                result.Add(normalized);
        }

        return result.Count > 0 ? result : null;
    }

    private static Vector2I?[]? ParseAutoTileVariants(Vector2IData?[]? variants, AutoTileFormat format)
    {
        if (variants == null || variants.Length == 0)
            return null;

        var expectedCount = format switch
        {
            AutoTileFormat.Blob47 => 47,
            AutoTileFormat.Edge16 => 16,
            AutoTileFormat.Corner16 => 16,
            _ => 16
        };
        var result = new Vector2I?[expectedCount];

        for (var i = 0; i < Math.Min(expectedCount, variants.Length); i++)
        {
            if (variants[i] != null)
                result[i] = new Vector2I(variants[i]!.X, variants[i]!.Y);
        }

        // Return null if no variants were actually set
        return result.Any(v => v.HasValue) ? result : null;
    }

    // JSON data model classes
    private sealed class TileRegistryData
    {
        [JsonPropertyName("version")] public string? Version { get; set; }

        [JsonPropertyName("tileset")] public string? Tileset { get; set; }

        [JsonPropertyName("biomes")] public Dictionary<string, BiomeData>? Biomes { get; set; }

        [JsonPropertyName("tiles")] public List<TileData>? Tiles { get; set; }
    }

    private sealed class TileData
    {
        [JsonPropertyName("id")] public string? Id { get; set; }

        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("passability")] public string? Passability { get; set; }

        [JsonPropertyName("atlasCoords")] public Vector2IData? AtlasCoords { get; set; }

        [JsonPropertyName("sourceId")] public int? SourceId { get; set; }

        [JsonPropertyName("layer")] public string? Layer { get; set; }

        [JsonPropertyName("elevation")] public float? Elevation { get; set; }

        [JsonPropertyName("isTransparent")] public bool? IsTransparent { get; set; }

        [JsonPropertyName("biomes")] public List<string>? Biomes { get; set; }

        [JsonPropertyName("size")] public Vector2IData? Size { get; set; }

        [JsonPropertyName("decorationDensity")] public float? DecorationDensity { get; set; }

        [JsonPropertyName("autoTileVariants")] public Vector2IData?[]? AutoTileVariants { get; set; }

        [JsonPropertyName("autoTileFormat")] public string? AutoTileFormat { get; set; }

        [JsonPropertyName("variations")] public Vector2IData[]? Variations { get; set; }

        [JsonPropertyName("variationMode")] public string? VariationMode { get; set; }

        [JsonPropertyName("animation")] public AnimationData? Animation { get; set; }

        [JsonPropertyName("dominance")] public int? Dominance { get; set; }

        [JsonPropertyName("innerTerrain")] public string? InnerTerrain { get; set; }

        [JsonPropertyName("outerTerrain")] public string? OuterTerrain { get; set; }

        [JsonPropertyName("description")] public string? Description { get; set; }
    }

    private sealed class AnimationData
    {
        [JsonPropertyName("frames")] public Vector2IData[]? Frames { get; set; }

        [JsonPropertyName("frameDuration")] public float FrameDuration { get; set; } = 0.2f;
    }

    private sealed class Vector2IData
    {
        [JsonPropertyName("x")] public int X { get; set; }

        [JsonPropertyName("y")] public int Y { get; set; }
    }
}
