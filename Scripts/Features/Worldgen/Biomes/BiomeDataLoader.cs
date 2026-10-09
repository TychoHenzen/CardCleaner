using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.Biomes;

/// <summary>
/// Reads the biome definitions from the biomes section of tiles.json. The tile section is not read here.
/// </summary>
public static class BiomeDataLoader
{
    /// <summary>
    /// Load biome definitions from a tiles JSON file. Returns no biomes when the file is missing or unreadable.
    /// </summary>
    public static Dictionary<string, BiomeData> LoadBiomes(string? path = null)
    {
        path ??= TileDataLoader.DefaultTilesPath;

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[BiomeDataLoader] File not found for biomes: {absolutePath}");
            return new Dictionary<string, BiomeData>();
        }

        try
        {
            return ParseBiomes(File.ReadAllText(absolutePath));
        }
        catch (Exception ex)
        {
            ILog.Print($"[BiomeDataLoader] Error reading biomes: {ex.Message}");
            return new Dictionary<string, BiomeData>();
        }
    }

    /// <summary>
    /// Parse the biomes section of tiles JSON text. Returns no biomes when the section is missing
    /// or the text does not parse.
    /// </summary>
    internal static Dictionary<string, BiomeData> ParseBiomes(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<BiomesFileData>(json, TileDataLoader.CreateJsonOptions());
            if (data?.Biomes == null || data.Biomes.Count == 0)
            {
                ILog.Print("[BiomeDataLoader] No biomes section found in JSON");
                return new Dictionary<string, BiomeData>();
            }

            ILog.Print($"[BiomeDataLoader] Loaded {data.Biomes.Count} biomes");
            return data.Biomes;
        }
        catch (Exception ex)
        {
            ILog.Print($"[BiomeDataLoader] Error parsing biomes: {ex.Message}");
            return new Dictionary<string, BiomeData>();
        }
    }

    private sealed class BiomesFileData
    {
        [JsonPropertyName("biomes")] public Dictionary<string, BiomeData>? Biomes { get; set; }
    }
}
