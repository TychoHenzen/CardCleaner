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
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Result of loading tile registry data
/// </summary>
public record TileRegistryResult(string TilesetPath, List<TileDefinition> Tiles);

/// <summary>
/// Configuration for blob-based terrain generation
/// </summary>
public record BlobGenerationConfig(
    bool Enabled = true,
    float NoiseScale = 0.15f,
    float ClusterStrength = 0.7f,
    int MinBlobSize = 3,
    int MaxBlobSize = 12);

/// <summary>
/// Loads tile definitions from JSON data files
/// </summary>
public static class TileDataLoader
{
    private const string DefaultTilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTilesetPath = "res://Assets/Terrain/TileSets/ByPack/FantasyDreamland.tres";

    private static readonly JsonSerializerOptions JsonOptions = new()
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
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, JsonOptions);
            if (data?.Tiles == null)
            {
                ILog.Print("[TileDataLoader] Invalid JSON structure");
                return new TileRegistryResult(DefaultTilesetPath, []);
            }

            var tilesetPath = data.Tileset ?? DefaultTilesetPath;
            var tiles = new List<TileDefinition>();
            foreach (var tileData in data.Tiles)
            {
                var tile = ConvertToTileDefinition(tileData);
                if (tile != null)
                    tiles.Add(tile);
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
    /// Load tiles only (backwards compatibility)
    /// </summary>
    public static List<TileDefinition> LoadTiles(string? path = null)
    {
        return LoadTileRegistry(path).Tiles;
    }

    /// <summary>
    /// Load blob generation config from JSON
    /// </summary>
    public static BlobGenerationConfig LoadBlobConfig(string? path = null)
    {
        path ??= DefaultTilesPath;

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TileDataLoader] File not found for blob config: {absolutePath}");
            return new BlobGenerationConfig();
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, JsonOptions);
            if (data == null)
            {
                ILog.Print("[TileDataLoader] Invalid JSON structure for blob config");
                return new BlobGenerationConfig();
            }

            return ParseBlobGenerationConfig(data.BlobGeneration);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TileDataLoader] Error loading blob config: {ex.Message}");
            return new BlobGenerationConfig();
        }
    }

    private static BlobGenerationConfig ParseBlobGenerationConfig(BlobGenerationData? data)
    {
        if (data == null)
            return new BlobGenerationConfig();

        return new BlobGenerationConfig(
            Enabled: data.Enabled,
            NoiseScale: data.NoiseScale,
            ClusterStrength: data.ClusterStrength,
            MinBlobSize: data.MinBlobSize,
            MaxBlobSize: data.MaxBlobSize);
    }

    /// <summary>
    ///     Load auto-tile configurations from JSON and create an AutoTileResolver
    /// </summary>
    public static AutoTileResolver LoadAutoTileResolver(string? path = null)
    {
        path ??= DefaultTilesPath;
        var resolver = new AutoTileResolver();

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TileDataLoader] File not found for auto-tile configs: {absolutePath}");
            return resolver;
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, JsonOptions);
            if (data?.AutoTileConfigs == null || data.AutoTileConfigs.Count == 0)
            {
                ILog.Print("[TileDataLoader] No auto-tile configs found");
                return resolver;
            }

            foreach (var configData in data.AutoTileConfigs)
            {
                if (string.IsNullOrEmpty(configData.BaseTileId))
                    continue;

                var config = new AutoTileConfig
                {
                    BaseTileId = configData.BaseTileId, DisplayName = configData.DisplayName ?? ""
                };

                if (configData.Variants != null)
                    for (var i = 0; i < Math.Min(16, configData.Variants.Length); i++)
                        config.Variants[i] = configData.Variants[i];

                resolver.Register(config);
            }

            ILog.Print($"[TileDataLoader] Loaded {resolver.ConfigCount} auto-tile configs");
            return resolver;
        }
        catch (Exception ex)
        {
            ILog.Print($"[TileDataLoader] Error loading auto-tile configs: {ex.Message}");
            return resolver;
        }
    }

    private static TileDefinition? ConvertToTileDefinition(TileData data)
    {
        if (string.IsNullOrEmpty(data.Id) || string.IsNullOrEmpty(data.Name))
            return null;

        var passability = ParsePassability(data.Passability);
        var layer = ParseLayer(data.Layer);
        var biomes = ParseBiomes(data.Biomes);
        var atlasCoords = new Vector2I(data.AtlasCoords?.X ?? 0, data.AtlasCoords?.Y ?? 0);
        var size = data.Size != null ? new Vector2I(data.Size.X, data.Size.Y) : (Vector2I?)null;
        var autoTileFormat = ParseAutoTileFormat(data.AutoTileFormat);

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
            blobSettings: data.BlobSettings != null ? ParseBlobGenerationConfig(data.BlobSettings) : null,
            autoTileVariants: ParseAutoTileVariants(data.AutoTileVariants, autoTileFormat),
            autoTileFormat: autoTileFormat);
    }

    private static AutoTileFormat ParseAutoTileFormat(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "blob47" => AutoTileFormat.Blob47,
            _ => AutoTileFormat.Corner16
        };
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

    private static HashSet<BiomeType>? ParseBiomes(List<string>? biomes)
    {
        if (biomes == null || biomes.Count == 0)
            return null;

        var result = new HashSet<BiomeType>();
        foreach (var biome in biomes)
        {
            var parsed = biome.ToLowerInvariant() switch
            {
                "plains" => BiomeType.Plains,
                "forest" => BiomeType.Forest,
                "desert" => BiomeType.Desert,
                "tundra" => BiomeType.Tundra,
                "swamp" => BiomeType.Swamp,
                "mountains" => BiomeType.Mountains,
                _ => (BiomeType?)null
            };

            if (parsed.HasValue)
                result.Add(parsed.Value);
        }

        return result.Count > 0 ? result : null;
    }

    private static Vector2I?[]? ParseAutoTileVariants(Vector2IData?[]? variants, AutoTileFormat format)
    {
        if (variants == null || variants.Length == 0)
            return null;

        var expectedCount = format == AutoTileFormat.Blob47 ? 47 : 16;
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

        [JsonPropertyName("tiles")] public List<TileData>? Tiles { get; set; }

        [JsonPropertyName("blobGeneration")] public BlobGenerationData? BlobGeneration { get; set; }

        [JsonPropertyName("autoTileConfigs")] public List<AutoTileConfigData>? AutoTileConfigs { get; set; }
    }

    private sealed class AutoTileConfigData
    {
        [JsonPropertyName("baseTileId")] public string? BaseTileId { get; set; }

        [JsonPropertyName("displayName")] public string? DisplayName { get; set; }

        [JsonPropertyName("variants")] public string?[]? Variants { get; set; }
    }

    private sealed class BlobGenerationData
    {
        [JsonPropertyName("enabled")] public bool Enabled { get; } = true;

        [JsonPropertyName("noiseScale")] public float NoiseScale { get; } = 0.15f;

        [JsonPropertyName("clusterStrength")] public float ClusterStrength { get; } = 0.7f;

        [JsonPropertyName("minBlobSize")] public int MinBlobSize { get; } = 3;

        [JsonPropertyName("maxBlobSize")] public int MaxBlobSize { get; } = 12;
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

        [JsonPropertyName("blobSettings")] public BlobGenerationData? BlobSettings { get; set; }

        [JsonPropertyName("autoTileVariants")] public Vector2IData?[]? AutoTileVariants { get; set; }

        [JsonPropertyName("autoTileFormat")] public string? AutoTileFormat { get; set; }
    }

    private sealed class Vector2IData
    {
        [JsonPropertyName("x")] public int X { get; set; }

        [JsonPropertyName("y")] public int Y { get; set; }
    }
}
