using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services.TilesetLoading.TileBuilding;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Builds TileDefinition objects from parsed TSX data.
/// </summary>
internal static class TileDefinitionBuilder
{
    /// <summary>
    /// Build TileDefinition objects from parsed TSX data.
    /// Creates tiles from Wang sets (using the set name as ID) and from tiles with explicit "id" property.
    /// </summary>
    public static List<TileDefinition> BuildTileDefinitions(
        Dictionary<int, TilePropertyData> tileProperties,
        WangSetData wangData,
        int columns,
        int sourceId)
    {
        var context = new TileBuildContext { WangData = wangData, Columns = columns, SourceId = sourceId };
        var tiles = new List<TileDefinition>();
        var processedWangSets = new HashSet<string>();

        // First pass: each Wang set becomes a tile with its 'class' attribute as the ID
        foreach (var (setId, setInfo) in wangData.WangSetInfo)
        {
            var tile = WangTileDefinitionBuilder.Build(setId, setInfo, tileProperties, context);
            if (tile == null)
                continue;

            tiles.Add(tile);
            processedWangSets.Add(ParseHelpers.ToSnakeCase(setId));
        }

        // Second pass: tiles from "type" attribute or explicit "id" property (for non-Wang tiles)
        foreach (var (tileId, propData) in tileProperties)
        {
            var id = ResolveTileId(propData);
            if (string.IsNullOrEmpty(id) || processedWangSets.Contains(id))
                continue;

            tiles.Add(PropertyTileDefinitionBuilder.Build(id, tileId, propData, context));
        }

        return tiles;
    }

    /// <summary>
    /// Prefer the 'type' attribute, fall back to the 'id' property for compatibility.
    /// </summary>
    private static string? ResolveTileId(TilePropertyData propData)
    {
        if (!string.IsNullOrEmpty(propData.Type))
            return propData.Type;

        return propData.Properties.TryGetValue("id", out var id) ? id : null;
    }
}
