#if TOOLS
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Godot;

namespace CardCleaner.Addons.TileEditor;

internal static class TileEditorJsonModels
{
    internal sealed class TileRegistryData
    {
        [JsonPropertyName("$schema")]
        public string? Schema { get; set; }

        [JsonPropertyName("version")]
        public string? Version { get; set; }

        [JsonPropertyName("tileset")]
        public string? Tileset { get; set; }

        [JsonPropertyName("biomes")]
        public Dictionary<string, BiomeDataJson>? Biomes { get; set; }

        [JsonPropertyName("autoTileFormats")]
        public List<AutoTileFormatDataJson>? AutoTileFormats { get; set; }

        [JsonPropertyName("tiles")]
        public List<TileData>? Tiles { get; set; }
    }

    internal sealed class AutoTileFormatDataJson
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("bitmaskType")]
        public string? BitmaskType { get; set; }

        [JsonPropertyName("allowedBitmasks")]
        public int[]? AllowedBitmasks { get; set; }

        [JsonPropertyName("variantSizes")]
        public Dictionary<string, Vector2IData>? VariantSizes { get; set; }
    }

    internal sealed class BiomeDataJson
    {
        [JsonPropertyName("displayName")]
        public string? DisplayName { get; set; }

        [JsonPropertyName("signature")]
        public float[]? Signature { get; set; }

        [JsonPropertyName("blockedPercentage")]
        public float BlockedPercentage { get; set; }

        [JsonPropertyName("passableTiles")]
        public Dictionary<string, float>? PassableTiles { get; set; }

        [JsonPropertyName("blockedTiles")]
        public Dictionary<string, float>? BlockedTiles { get; set; }
    }

    internal sealed class TileData
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

        [JsonPropertyName("autoTileVariants")]
        public Vector2IData?[]? AutoTileVariants { get; set; }

        [JsonPropertyName("decorationDensity")]
        public float? DecorationDensity { get; set; }

        [JsonPropertyName("autoTileFormat")]
        public string? AutoTileFormat { get; set; }

        [JsonPropertyName("variations")]
        public Vector2IData[]? Variations { get; set; }

        [JsonPropertyName("variationMode")]
        public string? VariationMode { get; set; }

        [JsonPropertyName("animation")]
        public AnimationData? Animation { get; set; }

        [JsonPropertyName("tileMode")]
        public string? TileMode { get; set; }

        [JsonPropertyName("dominance")]
        public int? Dominance { get; set; }

        [JsonPropertyName("sourceScale")]
        public float? SourceScale { get; set; }

        [JsonPropertyName("innerTerrain")]
        public string? InnerTerrain { get; set; }

        [JsonPropertyName("outerTerrain")]
        public string? OuterTerrain { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }
    }

    internal sealed class AnimationData
    {
        [JsonPropertyName("frames")]
        public Vector2IData[]? Frames { get; set; }

        [JsonPropertyName("frameDuration")]
        public float FrameDuration { get; set; } = 0.2f;
    }

    internal sealed class Vector2IData
    {
        [JsonPropertyName("x")]
        public int X { get; set; }

        [JsonPropertyName("y")]
        public int Y { get; set; }
    }
}
#endif
