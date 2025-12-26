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
/// Loads tile definitions from JSON data files
/// </summary>
public static class TileDataLoader
{
    private const string DefaultTilesPath = "res://Data/Tiles/tiles.json";

    public static List<TileDefinition> LoadTiles(string? path = null)
    {
        path ??= DefaultTilesPath;

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TileDataLoader] File not found: {absolutePath}");
            return [];
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, JsonOptions);
            if (data?.Tiles == null)
            {
                ILog.Print("[TileDataLoader] Invalid JSON structure");
                return [];
            }

            var tiles = new List<TileDefinition>();
            foreach (var tileData in data.Tiles)
            {
                var tile = ConvertToTileDefinition(tileData);
                if (tile != null)
                    tiles.Add(tile);
            }

            ILog.Print($"[TileDataLoader] Loaded {tiles.Count} tiles from {path}");
            return tiles;
        }
        catch (Exception ex)
        {
            ILog.Print($"[TileDataLoader] Error loading tiles: {ex.Message}");
            return [];
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

        return new TileDefinition(
            id: data.Id,
            name: data.Name,
            passability: passability,
            atlasCoords: atlasCoords,
            sourceId: data.SourceId ?? 4,
            layer: layer,
            elevation: data.Elevation ?? 0f,
            isTransparent: data.IsTransparent,
            allowedBiomes: biomes);
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
    private class TileRegistryData
    {
        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("tiles")]
        public List<TileData>? Tiles { get; set; }
    }

    private class TileData
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
    }

    private class Vector2IData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }
    }
}
