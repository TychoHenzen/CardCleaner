#if TOOLS
using System;
using System.Collections.Generic;
using System.Xml.Linq;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal sealed class TmxTsxLoader
{
    internal TsxLoadResult Load(string tsxPath, int targetTileSize)
    {
        var tiles = new List<TsxTileData>();
        var wangSets = new List<TsxWangSetData>();

        try
        {
            var tileset = XDocument.Load(tsxPath).Root;
            if (tileset == null || tileset.Name != "tileset")
                return Failure("Invalid TSX: missing tileset root", tiles, wangSets);

            var sourceResult = TmxTsxSourceReader.Read(tileset, tsxPath, targetTileSize);
            if (!sourceResult.Success)
                return Failure(sourceResult.Error, tiles, wangSets);

            var tileIndex = ReadTilePropertyIndex(tileset);
            var wangSetsElement = tileset.Element("wangsets");
            var wangTileIds = TmxWangSetReader.ReadTileIds(wangSetsElement);
            AddTiles(tiles, sourceResult.Source!, tileIndex, wangTileIds, tsxPath);
            TmxWangSetReader.AddWangSets(wangSets, wangSetsElement, sourceResult.Source!, tsxPath);
            return Success(tiles, wangSets);
        }
        catch (Exception ex)
        {
            return Failure(ex.Message, tiles, wangSets);
        }
    }

    internal static Dictionary<string, string> ParseProperties(XElement? propertiesElement)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (propertiesElement == null)
            return result;

        foreach (var property in propertiesElement.Elements("property"))
        {
            var name = property.Attribute("name")?.Value;
            var value = property.Attribute("value")?.Value ?? property.Value;
            if (!string.IsNullOrEmpty(name))
                result[name] = value;
        }

        return result;
    }

    internal static string GetString(
        Dictionary<string, string> properties,
        string key,
        string defaultValue)
        => properties.TryGetValue(key, out var value) ? value : defaultValue;

    internal static int GetInt(
        Dictionary<string, string> properties,
        string key,
        int defaultValue)
        => properties.TryGetValue(key, out var value) && int.TryParse(value, out var result)
            ? result
            : defaultValue;

    internal static bool GetBool(
        Dictionary<string, string> properties,
        string key,
        bool defaultValue)
        => properties.TryGetValue(key, out var value)
            ? value.ToLowerInvariant() is "true" or "1"
            : defaultValue;

    private static TilePropertyIndex ReadTilePropertyIndex(XElement tileset)
    {
        var index = new TilePropertyIndex();
        foreach (var tileElement in tileset.Elements("tile"))
        {
            var tileId = int.Parse(tileElement.Attribute("id")?.Value ?? "-1");
            if (tileId < 0)
                continue;

            index.Properties[tileId] = ParseProperties(tileElement.Element("properties"));
            var tileClass = tileElement.Attribute("class")?.Value
                            ?? tileElement.Attribute("type")?.Value;
            if (!string.IsNullOrEmpty(tileClass))
                index.Classes[tileId] = tileClass;
        }

        return index;
    }

    private static void AddTiles(
        List<TsxTileData> tiles,
        TsxSourceInfo source,
        TilePropertyIndex tileIndex,
        HashSet<int> wangTileIds,
        string tsxPath)
    {
        var tileIds = new HashSet<int>(tileIndex.Classes.Keys);
        tileIds.UnionWith(wangTileIds);
        foreach (var tileId in tileIds)
        {
            tileIndex.Properties.TryGetValue(tileId, out var properties);
            properties ??= new Dictionary<string, string>();
            var hasClassAttribute = tileIndex.Classes.ContainsKey(tileId);
            tiles.Add(new TsxTileData
            {
                TileId = tileId,
                TsxPath = tsxPath,
                AtlasX = tileId % source.Columns,
                AtlasY = tileId / source.Columns,
                SourceTileWidth = source.TileWidth,
                SourceTileHeight = source.TileHeight,
                SourceScale = source.SourceScale,
                SourceImage = source.Image,
                Properties = properties,
                Id = ResolveTileId(tileId, tileIndex, properties),
                Layer = wangTileIds.Contains(tileId) && !hasClassAttribute
                    ? "wang"
                    : GetString(properties, "layer", "terrain"),
                Dominance = GetInt(properties, "dominance", 0),
                HasExplicitId = properties.ContainsKey("id"),
                HasClassAttribute = hasClassAttribute
            });
        }
    }

    private static string ResolveTileId(
        int tileId,
        TilePropertyIndex tileIndex,
        Dictionary<string, string> properties)
    {
        if (tileIndex.Classes.TryGetValue(tileId, out var tileClass))
            return tileClass;
        if (properties.TryGetValue("id", out var propertyId))
            return propertyId;
        return $"tile_{tileId}";
    }

    private static TsxLoadResult Success(List<TsxTileData> tiles, List<TsxWangSetData> wangSets)
        => new() { Success = true, Tiles = tiles, WangSets = wangSets };

    private static TsxLoadResult Failure(
        string error,
        List<TsxTileData> tiles,
        List<TsxWangSetData> wangSets)
        => new() { Error = error, Tiles = tiles, WangSets = wangSets };
}
#endif
