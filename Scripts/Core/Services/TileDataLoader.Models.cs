using System.Collections.Generic;
using System.Text.Json.Serialization;
using CardCleaner.Scripts.Features.Worldgen.Biomes;

namespace CardCleaner.Scripts.Core.Services;

public static partial class TileDataLoader
{
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

        [JsonPropertyName("allowedBitmasks")] public List<int>? AllowedBitmasks { get; set; }

        [JsonPropertyName("variantSizes")] public Dictionary<string, Vector2IData>? VariantSizes { get; set; }

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

        [JsonPropertyName("isGapTile")] public bool? IsGapTile { get; set; }

        [JsonPropertyName("probability")] public float? Probability { get; set; }

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
}
