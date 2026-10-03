#if TOOLS
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal static class TmxWangSetReader
{
    internal static HashSet<int> ReadTileIds(XElement? wangSetsElement)
    {
        var tileIds = new HashSet<int>();
        if (wangSetsElement == null)
            return tileIds;

        foreach (var wangSetElement in wangSetsElement.Elements("wangset"))
        foreach (var wangTile in wangSetElement.Elements("wangtile"))
        {
            var tileId = int.Parse(wangTile.Attribute("tileid")?.Value ?? "-1");
            if (tileId >= 0)
                tileIds.Add(tileId);
        }

        return tileIds;
    }

    internal static void AddWangSets(
        List<TsxWangSetData> wangSets,
        XElement? wangSetsElement,
        TsxSourceInfo source,
        string tsxPath)
    {
        if (wangSetsElement == null)
            return;
        foreach (var wangSetElement in wangSetsElement.Elements("wangset"))
            wangSets.Add(ParseWangSet(wangSetElement, source, tsxPath));
    }

    private static TsxWangSetData ParseWangSet(
        XElement wangSetElement,
        TsxSourceInfo source,
        string tsxPath)
    {
        var properties = TmxTsxLoader.ParseProperties(wangSetElement.Element("properties"));
        var wangTiles = new Dictionary<int, List<int>>();
        var memberTileIds = new HashSet<int>();
        foreach (var wangTile in wangSetElement.Elements("wangtile"))
            AddWangTile(wangTile, wangSetElement, wangTiles, memberTileIds);

        return new TsxWangSetData
        {
            Name = GetName(wangSetElement),
            Type = GetAttributeValue(wangSetElement, "type", "corner"),
            TsxPath = tsxPath,
            Columns = source.Columns,
            SourceScale = source.SourceScale,
            Properties = properties,
            WangTiles = wangTiles,
            AllMemberTileIds = memberTileIds,
            SourceImage = source.Image,
            SourceTileWidth = source.TileWidth,
            SourceTileHeight = source.TileHeight,
            IsTransparent = TmxTsxLoader.GetBool(properties, "TransparentBackground", true),
            OuterTerrain = TmxTsxLoader.GetString(properties, "OuterTerrain", ""),
            InnerTerrain = TmxTsxLoader.GetString(properties, "InnerTerrain", "")
        };
    }

    private static void AddWangTile(
        XElement wangTile,
        XElement wangSetElement,
        Dictionary<int, List<int>> wangTiles,
        HashSet<int> memberTileIds)
    {
        var tileId = int.Parse(wangTile.Attribute("tileid")?.Value ?? "-1");
        if (tileId < 0)
            return;

        memberTileIds.Add(tileId);
        var wangId = wangTile.Attribute("wangid")?.Value ?? "";
        var bitmask = ParseWangId(wangId);
        var type = GetAttributeValue(wangSetElement, "type", "corner");
        if (bitmask < 0 || type == "mixed" && !IsBlobCompliant(wangId))
            return;

        if (!wangTiles.TryGetValue(bitmask, out var variants))
        {
            variants = new List<int>();
            wangTiles[bitmask] = variants;
        }

        variants.Add(tileId);
    }

    private static int ParseWangId(string wangId)
    {
        var values = ParseWangValues(wangId);
        if (values == null)
            return -1;

        return ToBit(values[1])
            | ToBit(values[3]) << 1
            | ToBit(values[5]) << 2
            | ToBit(values[7]) << 3;
    }

    private static bool IsBlobCompliant(string wangId)
    {
        var values = ParseWangValues(wangId);
        if (values == null)
            return false;

        return values[0] == EdgeFromCorners(values[7], values[1])
            && values[2] == EdgeFromCorners(values[1], values[3])
            && values[4] == EdgeFromCorners(values[3], values[5])
            && values[6] == EdgeFromCorners(values[5], values[7]);
    }

    private static int[]? ParseWangValues(string wangId)
    {
        var parts = wangId.Split(',');
        return parts.Length == 8 ? parts.Select(int.Parse).ToArray() : null;
    }

    private static int ToBit(int value)
        => value > 0 ? 1 : 0;

    private static int EdgeFromCorners(int first, int second)
        => first > 0 || second > 0 ? 1 : 0;

    private static string GetName(XElement wangSetElement)
    {
        var classAttribute = wangSetElement.Attribute("class");
        if (classAttribute != null)
            return classAttribute.Value;
        return GetAttributeValue(wangSetElement, "name", "unknown");
    }

    private static string GetAttributeValue(
        XElement element,
        string name,
        string defaultValue = "")
        => element.Attribute(name)?.Value ?? defaultValue;
}
#endif
