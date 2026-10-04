using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

public static partial class TileDataLoader
{
    private static AutoTileFormatDefinition? ParseCustomAutoTileFormat(AutoTileFormatData data)
    {
        if (string.IsNullOrWhiteSpace(data.Name))
            return null;

        var allowedBitmasks = ResolveAllowedBitmasks(data);
        if (allowedBitmasks == null || allowedBitmasks.Count == 0)
            return null;

        return new AutoTileFormatDefinition(
            name: data.Name,
            bitmaskType: ParseBitmaskType(data.BitmaskType),
            allowedBitmasks: allowedBitmasks,
            variantMappings: BuildVariantMappings(data, allowedBitmasks),
            isBuiltIn: false);
    }

    private static BitmaskType ParseBitmaskType(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "edge4" => BitmaskType.Edge4,
            "full8" => BitmaskType.Full8,
            _ => BitmaskType.Corner4
        };
    }

    private static HashSet<int>? ResolveAllowedBitmasks(AutoTileFormatData data)
    {
        return data.AllowedBitmasks?.ToHashSet()
            ?? data.Variants?
                .Where(variant => variant.AtlasCoords != null)
                .Select(variant => variant.Bitmask)
                .ToHashSet();
    }

    private static Dictionary<int, VariantDefinition> BuildVariantMappings(
        AutoTileFormatData data,
        HashSet<int> allowedBitmasks)
    {
        var mappings = new Dictionary<int, VariantDefinition>();
        foreach (var bitmask in allowedBitmasks)
            mappings[bitmask] = new VariantDefinition(Vector2I.Zero, GetVariantSize(data, bitmask));

        if (data.Variants == null)
            return mappings;

        foreach (var variant in data.Variants)
        {
            if (variant.AtlasCoords == null || !allowedBitmasks.Contains(variant.Bitmask))
                continue;

            mappings[variant.Bitmask] = CreateVariantDefinition(
                variant,
                mappings[variant.Bitmask].Size);
        }

        return mappings;
    }

    private static Vector2I GetVariantSize(AutoTileFormatData data, int bitmask)
    {
        if (data.VariantSizes?.TryGetValue(bitmask.ToString(), out var sizeData) == true && sizeData != null)
            return new Vector2I(sizeData.X, sizeData.Y);

        return Vector2I.One;
    }

    private static VariantDefinition CreateVariantDefinition(FormatVariantData data, Vector2I fallbackSize)
    {
        return new VariantDefinition(
            ToVector2I(data.AtlasCoords)!.Value,
            ToVector2I(data.Size) ?? fallbackSize,
            ToVector2I(data.Offset) ?? Vector2I.Zero,
            ToVector2I(data.AtlasRegionSize));
    }

    private static TileDefinition? ConvertToTileDefinition(TileData data, int fileIndex)
    {
        if (string.IsNullOrEmpty(data.Id) || string.IsNullOrEmpty(data.Name))
            return null;

        return CreateTileDefinition(data, fileIndex);
    }

    private static TileDefinition CreateTileDefinition(TileData data, int fileIndex)
    {
        var formatName = NormalizeAutoTileFormatName(data.AutoTileFormat);

        return new TileDefinition(
            data.Id!,
            data.Name!,
            ParsePassability(data.Passability),
            GetVector2OrDefault(data.AtlasCoords, Vector2I.Zero),
            new TileDefinitionOptions
            {
                SourceId = GetValueOrDefault(data.SourceId, 4),
                Layer = ParseLayer(data.Layer),
                Elevation = GetValueOrDefault(data.Elevation, 0f),
                IsTransparent = data.IsTransparent,
                AllowedBiomes = ParseBiomes(data.Biomes),
                Size = ToVector2I(data.Size),
                DecorationDensity = GetValueOrDefault(data.DecorationDensity, 1.0f),
                AutoTileVariants = ParseAutoTileVariants(data.AutoTileVariants, formatName),
                AutoTileFormatName = formatName,
                Variations = ParseVariations(data.Variations),
                VariationMode = ParseVariationMode(data.VariationMode),
                Animation = ParseAnimation(data.Animation),
                Dominance = GetValueOrDefault(data.Dominance, fileIndex),
                InnerTerrainId = data.InnerTerrain,
                OuterTerrainId = data.OuterTerrain,
                IsGapTile = GetValueOrDefault(data.IsGapTile, false),
                Probability = GetValueOrDefault(data.Probability, 1.0f)
            });
    }

    private static Vector2I? ToVector2I(Vector2IData? data)
    {
        return data == null ? null : new Vector2I(data.X, data.Y);
    }

    private static T GetValueOrDefault<T>(T? value, T defaultValue) where T : struct
    {
        return value.GetValueOrDefault(defaultValue);
    }

    private static Vector2I GetVector2OrDefault(Vector2IData? data, Vector2I defaultValue)
    {
        return ToVector2I(data).GetValueOrDefault(defaultValue);
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
}
