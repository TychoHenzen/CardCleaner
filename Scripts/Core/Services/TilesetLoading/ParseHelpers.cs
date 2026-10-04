using System;
using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Shared parsing helper methods for Tiled tileset loading.
/// </summary>
internal static class ParseHelpers
{
    public static int ParseInt(string? value, int defaultValue)
        => int.TryParse(value, out var result) ? result : defaultValue;

    public static float ParseFloat(string? value, float defaultValue)
        => float.TryParse(value, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : defaultValue;

    public static bool ParseBool(string? value, bool defaultValue)
    {
        if (string.IsNullOrEmpty(value)) return defaultValue;
        return value.ToLowerInvariant() switch
        {
            "true" or "1" or "yes" => true,
            "false" or "0" or "no" => false,
            _ => defaultValue
        };
    }

    public static string GetString(Dictionary<string, string> props, string key, string defaultValue)
        => props.GetValueOrDefault(key, defaultValue);

    public static string? GetStringOrNull(Dictionary<string, string> props, string key)
        => props.GetValueOrDefault(key);

    public static int GetInt(Dictionary<string, string> props, string key, int defaultValue)
        => props.TryGetValue(key, out var v) ? ParseInt(v, defaultValue) : defaultValue;

    public static float GetFloat(Dictionary<string, string> props, string key, float defaultValue)
        => props.TryGetValue(key, out var v) ? ParseFloat(v, defaultValue) : defaultValue;

    public static bool GetBool(Dictionary<string, string> props, string key, bool defaultValue)
        => props.TryGetValue(key, out var v) ? ParseBool(v, defaultValue) : defaultValue;

    public static Vector2I TileIdToAtlasCoords(int tileId, int columns)
    {
        if (columns <= 0) columns = 1;
        return new Vector2I(tileId % columns, tileId / columns);
    }

    public static TilePassability ParsePassability(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "passable" => TilePassability.Passable,
            "solid" => TilePassability.Solid,
            "partially_passable" or "partial" => TilePassability.PartiallyPassable,
            _ => TilePassability.Passable
        };
    }

    public static TileLayer ParseLayer(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "terrain" => TileLayer.Terrain,
            "decoration" => TileLayer.Decoration,
            "structure" => TileLayer.Structure,
            "effects" => TileLayer.Effects,
            _ => TileLayer.Terrain
        };
    }

    public static VariationMode ParseVariationMode(string value)
    {
        return value.ToLowerInvariant() switch
        {
            "pergeneration" or "per_generation" => VariationMode.PerGeneration,
            "contextual" => VariationMode.Contextual,
            _ => VariationMode.PerInstance
        };
    }

    /// <summary>
    /// Convert a name to snake_case (e.g., "Grass3" → "grass3", "ForestFloor" → "forest_floor").
    /// </summary>
    public static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c) && i > 0 && !char.IsUpper(name[i - 1]))
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }

    /// <summary>
    /// Parse variations string like "3,4;5,6;7,8" into coordinates.
    /// </summary>
    public static Vector2I[]? ParseVariationsString(string str, int columns)
    {
        var variations = new List<Vector2I>();

        foreach (var part in str.Split(';'))
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            // Could be "x,y" coords or just a tile id
            if (trimmed.Contains(','))
            {
                var coords = trimmed.Split(',');
                if (coords.Length == 2)
                {
                    var x = ParseInt(coords[0], 0);
                    var y = ParseInt(coords[1], 0);
                    variations.Add(new Vector2I(x, y));
                }
            }
            else
            {
                // Treat as tile ID
                var tileId = ParseInt(trimmed, -1);
                if (tileId >= 0)
                    variations.Add(TileIdToAtlasCoords(tileId, columns));
            }
        }

        return variations.Count > 0 ? variations.ToArray() : null;
    }

    /// <summary>
    /// Known biome IDs in bit position order (matches Tiled project enum).
    /// </summary>
    private static readonly string[] KnownBiomeIds =
    {
        "plains", "forest", "desert", "tundra", "swamp",
        "mountains", "water", "cave", "volcanic", "magical"
    };

    /// <summary>
    /// Parse biome properties. Supports both:
    /// - Integer "biome" property (flags enum/bitfield from Tiled): 0 = all biomes, non-zero = selected biomes
    /// - Boolean "biome_*" properties (biome_forest: true, etc.): fallback for manual property editing
    /// </summary>
    public static HashSet<string>? ParseBiomeBooleans(Dictionary<string, string> props)
    {
        // First, check for integer biome flags property (from Tiled enum)
        if (props.TryGetValue("biome", out var biomeValue))
        {
            if (int.TryParse(biomeValue, out var biomeFlags))
            {
                // biome=0 means "all biomes" (universal tile) - return null
                if (biomeFlags == 0)
                    return null;

                // Decode flags to biome IDs
                var biomes = new HashSet<string>();
                for (var i = 0; i < KnownBiomeIds.Length; i++)
                {
                    if ((biomeFlags & (1 << i)) != 0)
                        biomes.Add(KnownBiomeIds[i]);
                }

                return biomes.Count > 0 ? biomes : null;
            }
        }

        // Fallback: check for boolean biome_* properties
        var boolBiomes = new HashSet<string>();

        foreach (var (key, value) in props)
        {
            if (!key.StartsWith("biome_", StringComparison.OrdinalIgnoreCase))
                continue;

            if (ParseBool(value, false))
            {
                var biomeName = key.Substring(6).ToLowerInvariant(); // Remove "biome_" prefix
                boolBiomes.Add(biomeName);
            }
        }

        return boolBiomes.Count > 0 ? boolBiomes : null;
    }
}
