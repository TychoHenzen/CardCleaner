using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services.TilesetLoading.TileProperties;
using System.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;

/// <summary>
/// Builds a TileDefinition from explicit tile properties (for non-Wang-set tiles).
/// </summary>
internal static class PropertyTileDefinitionBuilder
{
    internal static TileDefinition Build(
        string id,
        int baseTileId,
        TilePropertyData propData,
        TileBuildContext context)
    {
        var props = propData.Properties;
        var wangData = context.WangData;

        // Tiles without Wang set membership are gap tiles by default (base tiles for background layer)
        var hasWangSet = wangData.TileToWangSet.TryGetValue(baseTileId, out var wangSetName);
        var autoTileFormat = ParseHelpers.GetString(props, "autotileformat", "corner16");
        Vector2I?[]? autoTileVariants = null;

        if (hasWangSet)
        {
            var bitmaskType = wangData.TileBitmaskType.GetValueOrDefault(baseTileId, BitmaskType.Corner4);
            autoTileFormat = FormatNameFor(bitmaskType, autoTileFormat);
            autoTileVariants = WangSetTileLookup.BuildAutoTileVariants(wangSetName!, context);
        }

        return new TileDefinitionSpec
        {
            Id = id,
            Name = ParseHelpers.GetString(props, "name", id),
            AtlasCoords = ParseHelpers.TileIdToAtlasCoords(baseTileId, context.Columns),
            SourceId = context.SourceId,
            Common = TilePropertyReader.ReadCommon(props),
            Biomes = ParseHelpers.ParseBiomeBooleans(props),
            Size = TilePropertyReader.ReadSize(props),
            AutoTileFormat = autoTileFormat,
            AutoTileVariants = autoTileVariants,
            Variations = ReadVariations(propData, context.Columns),
            VariationMode = ParseHelpers.ParseVariationMode(
                ParseHelpers.GetString(props, "variationmode", "perinstance")),
            Animation = ReadAnimation(propData, context.Columns),
            IsGapTile = ParseHelpers.GetBool(props, "isgaptile", !hasWangSet),
            Probability = ParseHelpers.GetFloat(props, "probability", 1f)
        }.ToDefinition();
    }

    private static string FormatNameFor(BitmaskType bitmaskType, string fallback)
    {
        return bitmaskType switch
        {
            BitmaskType.Corner4 => "corner16",
            BitmaskType.Edge4 => "edge16",
            BitmaskType.Full8 => "blob47",
            _ => fallback
        };
    }

    private static Vector2I[]? ReadVariations(TilePropertyData propData, int columns)
    {
        var variationsStr = ParseHelpers.GetStringOrNull(propData.Properties, "variations");
        return string.IsNullOrEmpty(variationsStr)
            ? null
            : ParseHelpers.ParseVariationsString(variationsStr, columns);
    }

    private static TileAnimation? ReadAnimation(TilePropertyData propData, int columns)
    {
        if (propData.Animation == null || propData.Animation.Count == 0)
            return null;

        var frames = propData.Animation
            .Select(f => ParseHelpers.TileIdToAtlasCoords(f.TileId, columns))
            .ToArray();
        return new TileAnimation(frames, propData.Animation[0].Duration);
    }
}
