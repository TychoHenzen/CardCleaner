using System;
using System.IO;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Loads TMX map files with full tile resolution support.
/// </summary>
internal static class TmxMapLoader
{
    /// <summary>
    /// Loads a TMX map file with full tile resolution support.
    /// </summary>
    internal static TmxMapData? LoadTmxMap(string tmxPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tmxPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TmxMapLoader] TMX file not found: {absolutePath}");
            return null;
        }

        try
        {
            return ReadMap(absolutePath);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TmxMapLoader] Error loading TMX map: {ex.Message}");
            return null;
        }
    }

    private static TmxMapData? ReadMap(string absolutePath)
    {
        var map = XDocument.Load(absolutePath).Root;
        if (map == null || map.Name != "map")
        {
            ILog.Print("[TmxMapLoader] Invalid TMX: missing map root element");
            return null;
        }

        var tmxDir = Path.GetDirectoryName(absolutePath) ?? "";
        var mapWidth = ParseHelpers.ParseInt(map.Attribute("width")?.Value, 0);
        var mapHeight = ParseHelpers.ParseInt(map.Attribute("height")?.Value, 0);

        var tilesets = TmxTilesetReferenceReader.ReadReferences(map, tmxDir);
        var chunks = TmxChunkReader.ReadChunks(map);

        ILog.Print($"[TmxMapLoader] Loaded TMX map: {chunks.Count} chunks, {tilesets.Count} tilesets");
        return new TmxMapData(new Vector2I(mapWidth, mapHeight), tilesets, chunks);
    }
}
