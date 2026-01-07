using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Result of loading tile registry data
/// </summary>
public record TileRegistryResult(string TilesetPath, List<TileDefinition> Tiles, TilesetConfig TilesetConfig);

/// <summary>
/// Loads tile definitions from JSON data files or Tiled TSX files.
/// </summary>
public static class TileDataLoader
{
    private const string DefaultTilesPath = "res://Data/Tiles/tiles.json";
    private const string DefaultTiledPath = "res://Data/Tiled/tileset.tmx";
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
    /// Load tiles from TSX/TMX file, JSON file, or auto-detect based on available files.
    /// Prefers Tiled format (TMX/TSX) if DefaultTiledPath exists, otherwise falls back to JSON.
    /// </summary>
    public static TileRegistryResult LoadTileRegistry(string? path = null)
    {
        // Auto-detect: prefer Tiled format if available and no explicit path given
        if (path == null)
        {
            var tiledAbsolutePath = ProjectSettings.GlobalizePath(DefaultTiledPath);
            if (File.Exists(tiledAbsolutePath))
            {
                ILog.Print($"[TileDataLoader] Tiled file found, loading from {DefaultTiledPath}");
                return DefaultTiledPath.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase)
                    ? LoadFromTmx(DefaultTiledPath)
                    : LoadFromTsx(DefaultTiledPath);
            }
            path = DefaultTilesPath;
        }

        // Dispatch based on file extension
        if (path.EndsWith(".tsx", StringComparison.OrdinalIgnoreCase))
            return LoadFromTsx(path);
        if (path.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
            return LoadFromTmx(path);

        return LoadFromJson(path);
    }

    /// <summary>
    /// Load tiles from Tiled TSX tileset file.
    /// </summary>
    public static TileRegistryResult LoadFromTsx(string tsxPath, int sourceId = 0)
    {
        return TiledTilesetLoader.LoadFromTsx(tsxPath, sourceId);
    }

    /// <summary>
    /// Load tiles from Tiled TMX map file (loads all referenced tilesets).
    /// </summary>
    public static TileRegistryResult LoadFromTmx(string tmxPath)
    {
        return TiledTilesetLoader.LoadFromTmx(tmxPath);
    }

    /// <summary>
    /// Load tiles and tileset path from JSON (original format).
    /// </summary>
    public static TileRegistryResult LoadFromJson(string? path = null)
    {
        path ??= DefaultTilesPath;

        var absolutePath = ProjectSettings.GlobalizePath(path);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TileDataLoader] File not found: {absolutePath}");
            return new TileRegistryResult(DefaultTilesetPath, [], TilesetConfig.Default);
        }

        try
        {
            var json = File.ReadAllText(absolutePath);
            var data = JsonSerializer.Deserialize<TileRegistryData>(json, CreateJsonOptions());
            if (data?.Tiles == null)
            {
                ILog.Print("[TileDataLoader] Invalid JSON structure");
                return new TileRegistryResult(DefaultTilesetPath, [], TilesetConfig.Default);
            }

            // Parse tileset config (with defaults if not present)
            var tilesetConfig = ParseTilesetConfig(data.TilesetConfig);

            // Register custom auto-tile formats before processing tiles
            RegisterCustomAutoTileFormats(data.AutoTileFormats);

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
            return new TileRegistryResult(tilesetPath, tiles, tilesetConfig);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TileDataLoader] Error loading tiles: {ex.Message}");
            return new TileRegistryResult(DefaultTilesetPath, [], TilesetConfig.Default);
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

    /// <summary>
    /// Parse tileset configuration from JSON data, with defaults for missing values.
    /// </summary>
    private static TilesetConfig ParseTilesetConfig(TilesetConfigData? data)
    {
        if (data == null)
            return TilesetConfig.Default;

        return new TilesetConfig
        {
            BaseTileSize = data.BaseTileSize != null
                ? new Vector2I(data.BaseTileSize.X, data.BaseTileSize.Y)
                : new Vector2I(16, 16),
            GridOffset = data.GridOffset != null
                ? new Vector2((float)data.GridOffset.X, (float)data.GridOffset.Y)
                : Vector2.Zero
        };
    }

    /// <summary>
    /// Register custom auto-tile formats from JSON data.
    /// </summary>
    private static void RegisterCustomAutoTileFormats(List<AutoTileFormatData>? formats)
    {
        // Ensure built-in formats are registered first
        AutoTileFormatRegistry.EnsureBuiltInsRegistered();

        if (formats == null || formats.Count == 0)
            return;

        foreach (var formatData in formats)
        {
            if (string.IsNullOrWhiteSpace(formatData.Name))
            {
                ILog.Print("[TileDataLoader] Skipping custom format with missing name");
                continue;
            }

            // Don't allow overriding built-in formats
            if (AutoTileFormatRegistry.Contains(formatData.Name))
            {
                ILog.Print($"[TileDataLoader] Skipping custom format '{formatData.Name}' - name already registered");
                continue;
            }

            var format = ParseCustomAutoTileFormat(formatData);
            if (format != null)
            {
                AutoTileFormatRegistry.Register(format);
                ILog.Print($"[TileDataLoader] Registered custom auto-tile format: {formatData.Name}");
            }
        }
    }

    /// <summary>
    /// Parse a single custom auto-tile format from JSON data.
    /// </summary>
    private static AutoTileFormatDefinition? ParseCustomAutoTileFormat(AutoTileFormatData data)
    {
        if (string.IsNullOrWhiteSpace(data.Name) || data.Variants == null || data.Variants.Count == 0)
            return null;

        var bitmaskType = data.BitmaskType?.ToLowerInvariant() switch
        {
            "edge4" => BitmaskType.Edge4,
            "full8" => BitmaskType.Full8,
            _ => BitmaskType.Corner4
        };

        var allowedBitmasks = new HashSet<int>();
        var variantMappings = new Dictionary<int, VariantDefinition>();

        foreach (var variant in data.Variants)
        {
            if (variant.AtlasCoords == null)
                continue;

            var bitmask = variant.Bitmask;
            allowedBitmasks.Add(bitmask);

            var atlasCoords = new Vector2I(variant.AtlasCoords.X, variant.AtlasCoords.Y);
            var size = variant.Size != null
                ? new Vector2I(variant.Size.X, variant.Size.Y)
                : Vector2I.One;
            var offset = variant.Offset != null
                ? new Vector2I(variant.Offset.X, variant.Offset.Y)
                : Vector2I.Zero;
            var atlasRegionSize = variant.AtlasRegionSize != null
                ? new Vector2I(variant.AtlasRegionSize.X, variant.AtlasRegionSize.Y)
                : (Vector2I?)null;

            variantMappings[bitmask] = new VariantDefinition(atlasCoords, size, offset, atlasRegionSize);
        }

        return new AutoTileFormatDefinition(
            name: data.Name,
            bitmaskType: bitmaskType,
            allowedBitmasks: allowedBitmasks,
            variantMappings: variantMappings,
            isBuiltIn: false);
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
        var autoTileFormatName = NormalizeAutoTileFormatName(data.AutoTileFormat);
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
            autoTileVariants: ParseAutoTileVariants(data.AutoTileVariants, autoTileFormatName),
            autoTileFormatName: autoTileFormatName,
            variations: variations,
            variationMode: variationMode,
            animation: animation,
            dominance: dominance,
            innerTerrainId: data.InnerTerrain,
            outerTerrainId: data.OuterTerrain);
    }

    /// <summary>
    /// Normalizes the auto-tile format name to lowercase, defaulting to "corner16".
    /// </summary>
    private static string NormalizeAutoTileFormatName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "corner16" : value.ToLowerInvariant();
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

    private static Vector2I?[]? ParseAutoTileVariants(Vector2IData?[]? variants, string formatName)
    {
        if (variants == null || variants.Length == 0)
            return null;

        // Get expected count from registry, defaulting to 16
        var expectedCount = 16;
        if (AutoTileFormatRegistry.TryGet(formatName, out var format) && format != null)
        {
            expectedCount = format.GetExpectedVariantCount();
        }

        var result = new Vector2I?[expectedCount];

        for (var i = 0; i < Math.Min(expectedCount, variants.Length); i++)
        {
            if (variants[i] != null)
                result[i] = new Vector2I(variants[i]!.X, variants[i]!.Y);
        }

        // Return null if no variants were actually set
        return result.Any(v => v.HasValue) ? result : null;
    }

    #region JSON Data Model Classes

    private sealed class TileRegistryData
    {
        [JsonPropertyName("version")] public string? Version { get; set; }

        [JsonPropertyName("tileset")] public string? Tileset { get; set; }

        [JsonPropertyName("tilesetConfig")] public TilesetConfigData? TilesetConfig { get; set; }

        [JsonPropertyName("autoTileFormats")] public List<AutoTileFormatData>? AutoTileFormats { get; set; }

        [JsonPropertyName("biomes")] public Dictionary<string, BiomeData>? Biomes { get; set; }

        [JsonPropertyName("tiles")] public List<TileData>? Tiles { get; set; }
    }

    private sealed class TilesetConfigData
    {
        [JsonPropertyName("baseTileSize")] public Vector2IData? BaseTileSize { get; set; }

        [JsonPropertyName("gridOffset")] public Vector2Data? GridOffset { get; set; }
    }

    private sealed class AutoTileFormatData
    {
        [JsonPropertyName("name")] public string? Name { get; set; }

        [JsonPropertyName("bitmaskType")] public string? BitmaskType { get; set; }

        [JsonPropertyName("variants")] public List<FormatVariantData>? Variants { get; set; }
    }

    private sealed class FormatVariantData
    {
        [JsonPropertyName("bitmask")] public int Bitmask { get; set; }

        [JsonPropertyName("atlasCoords")] public Vector2IData? AtlasCoords { get; set; }

        [JsonPropertyName("size")] public Vector2IData? Size { get; set; }

        [JsonPropertyName("offset")] public Vector2IData? Offset { get; set; }

        [JsonPropertyName("atlasRegionSize")] public Vector2IData? AtlasRegionSize { get; set; }
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

    private sealed class Vector2Data
    {
        [JsonPropertyName("x")] public double X { get; set; }

        [JsonPropertyName("y")] public double Y { get; set; }
    }

    #endregion
}
