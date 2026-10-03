using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;

/// <summary>
/// Builds a TileDefinition from a Wang set.
/// Uses the Wang set 'class' attribute (setId) as the tile ID and 'name' for display.
/// </summary>
internal static class WangTileDefinitionBuilder
{
    internal static TileDefinition? Build(
        string setId,
        WangSetInfo setInfo,
        Dictionary<int, TilePropertyData> tileProperties,
        TileBuildContext context)
    {
        var representative = WangSetTileLookup.FindRepresentativeTile(setId, context.WangData);
        if (representative == null)
        {
            ILog.Print($"[TileDefinitionBuilder] Wang set '{setId}' has no tiles, skipping");
            return null;
        }

        var props = MergeProperties(setInfo, tileProperties, representative.TileId);

        return new TileDefinitionSpec
        {
            Id = ParseHelpers.ToSnakeCase(setId),
            Name = ParseHelpers.GetString(props, "name", setInfo.Name),
            AtlasCoords = ParseHelpers.TileIdToAtlasCoords(representative.TileId, context.Columns),
            SourceId = context.SourceId,
            Common = TilePropertyReader.ReadCommon(props),
            Biomes = ParseHelpers.ParseBiomeBooleans(props),
            Size = TilePropertyReader.ReadSize(props),
            // Dual-grid only samples 4 corners, so always use corner16 format
            // Blob47/mixed tilesets are converted to corner16 at load time
            AutoTileFormat = "corner16",
            AutoTileVariants = WangSetTileLookup.BuildAutoTileVariants(setId, context),
            IsGapTile = ParseHelpers.GetBool(props, "isgaptile", false),
            Probability = representative.Probability
        }.ToDefinition();
    }

    /// <summary>
    /// Wang set properties, with fallback to the representative tile properties.
    /// </summary>
    private static Dictionary<string, string> MergeProperties(
        WangSetInfo setInfo,
        Dictionary<int, TilePropertyData> tileProperties,
        int representativeTileId)
    {
        var props = new Dictionary<string, string>(setInfo.Properties, System.StringComparer.OrdinalIgnoreCase);
        if (!tileProperties.TryGetValue(representativeTileId, out var tilePropData))
            return props;

        foreach (var (key, value) in tilePropData.Properties)
        {
            if (!props.ContainsKey(key))
                props[key] = value;
        }

        return props;
    }
}
