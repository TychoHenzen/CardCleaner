using System.Xml.Linq;
using CardCleaner.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Core.Services.TilesetLoading.WangSets;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Parses Wang set data from Tiled TSX tileset files.
/// Converts Tiled's wangid format to bitmasks for auto-tiling.
/// </summary>
internal static class WangSetParser
{
    private const int DefaultTerrainColorIndex = 1;

    /// <summary>
    /// Parse all Wang sets and build bitmask -> tile mappings.
    /// Multiple tiles per bitmask = variations.
    /// </summary>
    public static WangSetData ParseWangSets(XElement tileset)
    {
        var result = new WangSetData();
        var wangsets = tileset.Element("wangsets");
        if (wangsets == null) return result;

        foreach (var wangset in wangsets.Elements("wangset"))
        {
            var context = RegisterWangSet(wangset, result);
            foreach (var wangtile in wangset.Elements("wangtile"))
            {
                RegisterWangTile(wangtile, context, result);
            }
        }

        return result;
    }

    private static WangSetContext RegisterWangSet(XElement wangset, WangSetData result)
    {
        var wangType = wangset.Attribute("type")?.Value ?? "corner";
        // Use 'class' attribute as the tile ID, fall back to 'name' for compatibility
        var setId = wangset.Attribute("class")?.Value ?? wangset.Attribute("name")?.Value ?? "unnamed";
        var setName = wangset.Attribute("name")?.Value ?? setId;

        // Wangset properties may override which color index represents "terrain present"
        var setProps = TilePropertyParser.ParseProperties(wangset.Element("properties"));
        var terrainColorIndex = setProps.TryGetValue("terraincolor", out var tcVal)
            ? ParseHelpers.ParseInt(tcVal, DefaultTerrainColorIndex)
            : DefaultTerrainColorIndex;

        var bitmaskType = ToBitmaskType(wangType);

        // setId is the tile ID (from class attr), setName is the display name
        result.WangSetInfo[setId] = new WangSetInfo(setName, bitmaskType, setProps);

        return new WangSetContext(setId, wangType, bitmaskType, terrainColorIndex);
    }

    private static BitmaskType ToBitmaskType(string wangType) => wangType switch
    {
        "corner" => BitmaskType.Corner4,
        "edge" => BitmaskType.Edge4,
        "mixed" => BitmaskType.Full8,
        _ => BitmaskType.Corner4
    };

    private static void RegisterWangTile(XElement wangtile, WangSetContext context, WangSetData result)
    {
        var tileId = ParseHelpers.ParseInt(wangtile.Attribute("tileid")?.Value, -1);
        var wangidStr = wangtile.Attribute("wangid")?.Value ?? "";
        if (tileId < 0 || string.IsNullOrEmpty(wangidStr)) return;

        var probability = ParseHelpers.ParseFloat(wangtile.Attribute("probability")?.Value, 1f);
        var wangId = WangId.Parse(wangidStr, context.TerrainColorIndex);

        // Track which tiles belong to this wang set (use setId for lookups).
        // This is needed for gap tile detection regardless of blob compliance.
        result.TileToWangSet.TryAdd(tileId, context.SetId);
        result.TileBitmaskType.TryAdd(tileId, context.BitmaskType);

        // For mixed wangsets (used as Corner16), only add tiles whose edges match what the
        // blob constraint would derive from their corners. This picks the canonical tile
        // for each corner combination.
        if (context.WangType == "mixed" && wangId?.IsBlobCompliant() != true)
            return;

        var bitmask = wangId?.ToBitmask(context.WangType) ?? 0;
        var key = (context.SetId, bitmask);
        if (!result.BitmaskToTiles.TryGetValue(key, out var tiles))
        {
            tiles = [];
            result.BitmaskToTiles[key] = tiles;
        }

        tiles.Add(new WangTileInfo(tileId, probability));
    }
}
