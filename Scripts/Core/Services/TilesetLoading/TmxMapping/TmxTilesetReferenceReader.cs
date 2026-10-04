using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;

/// <summary>
/// Resolves the external TSX tileset references of a TMX map.
/// </summary>
internal static class TmxTilesetReferenceReader
{
    internal static List<TmxTilesetReference> ReadReferences(XElement map, string tmxDir)
    {
        var tilesets = new List<TmxTilesetReference>();
        foreach (var tilesetRef in map.Elements("tileset"))
        {
            var reference = ReadReference(tilesetRef, tmxDir);
            if (reference != null)
                tilesets.Add(reference);
        }

        return tilesets;
    }

    private static TmxTilesetReference? ReadReference(XElement tilesetRef, string tmxDir)
    {
        var firstGid = ParseHelpers.ParseInt(tilesetRef.Attribute("firstgid")?.Value, 1);
        var source = tilesetRef.Attribute("source")?.Value;

        // Skip internal/builtin tilesets (e.g., ":/" paths)
        if (string.IsNullOrEmpty(source) || source.StartsWith(":/"))
            return null;

        var tsxAbsolutePath = Path.GetFullPath(Path.Combine(tmxDir, source));
        if (!File.Exists(tsxAbsolutePath))
        {
            ILog.Print($"[TmxMapLoader] TSX file not found: {tsxAbsolutePath}");
            return null;
        }

        // Load TSX to get tile count and columns
        var tileset = XDocument.Load(tsxAbsolutePath).Root;
        var tileCount = ParseHelpers.ParseInt(tileset?.Attribute("tilecount")?.Value, 0);
        var columns = ParseHelpers.ParseInt(tileset?.Attribute("columns")?.Value, 1);
        var lastGid = firstGid + tileCount - 1;

        var tilesetData = TiledTilesetLoader.LoadFromTsx(tsxAbsolutePath, 0);
        return new TmxTilesetReference(firstGid, lastGid, tsxAbsolutePath, tilesetData, columns);
    }
}
