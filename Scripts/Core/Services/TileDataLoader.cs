using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

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

    private static TileDefinition? ConvertToTileDefinition(TileData data)
    {
        if (string.IsNullOrEmpty(data.Id) || string.IsNullOrEmpty(data.Name))
            return null;

        var passability = ParsePassability(data.Passability);
        var layer = ParseLayer(data.Layer);
        var biomes = ParseBiomes(data.Biomes);
        var atlasCoords = new Vector2I(data.AtlasCoords?.X ?? 0, data.AtlasCoords?.Y ?? 0);
        var size = data.Size != null ? new Vector2I(data.Size.X, data.Size.Y) : (Vector2I?)null;

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
            size: size);
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // JSON data model classes
    private sealed class TileRegistryData
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("tileset")]
        public string? Tileset { get; set; }

        [JsonPropertyName("tiles")]
        public List<TileData>? Tiles { get; set; }
    }

    private sealed class TileData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("passability")]
        public string? Passability { get; set; }

        [JsonPropertyName("atlasCoords")]
        public Vector2IData? AtlasCoords { get; set; }

        [JsonPropertyName("sourceId")]
        public int? SourceId { get; set; }

        [JsonPropertyName("layer")]
        public string? Layer { get; set; }

        [JsonPropertyName("elevation")]
        public float? Elevation { get; set; }

        [JsonPropertyName("isTransparent")]
        public bool? IsTransparent { get; set; }

        [JsonPropertyName("biomes")]
        public List<string>? Biomes { get; set; }

        [JsonPropertyName("size")]
        public Vector2IData? Size { get; set; }
    }

    private sealed class Vector2IData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }
    }
}
