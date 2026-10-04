#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Inserts required custom properties into TSX tileset files with sensible defaults.
/// Preserves existing property values.
/// </summary>
internal static class TsxPropertyInserter
{
    /// <summary>
    /// Inserts missing required properties into a single TSX file.
    /// </summary>
    /// <param name="tsxPath">Path to the TSX file</param>
    /// <param name="insertTileProperties">Whether to insert tile properties (for tiles with id property)</param>
    /// <param name="insertWangSetProperties">Whether to insert wang set properties</param>
    /// <returns>Result with counts of modified tiles/wangsets</returns>
    internal static TsxInsertResult InsertProperties(
        string tsxPath,
        bool insertTileProperties = true,
        bool insertWangSetProperties = true)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tsxPath);
        if (!File.Exists(absolutePath))
            return new TsxInsertResult(false, $"TSX file not found: {absolutePath}", 0, 0);

        try
        {
            var doc = XDocument.Load(absolutePath);
            var tileset = doc.Root;
            if (tileset == null || tileset.Name != "tileset")
                return new TsxInsertResult(false, "Invalid TSX: missing tileset root", 0, 0);

            var tilesModified = insertTileProperties ? InsertIntoTiles(tileset) : 0;
            var wangSetsModified = insertWangSetProperties ? InsertIntoWangSets(tileset) : 0;
            var result = new TsxInsertResult(true, "", tilesModified, wangSetsModified);

            // Only save if modifications were made
            if (tilesModified > 0 || wangSetsModified > 0)
                SaveModified(doc, tsxPath, result);

            return result;
        }
        catch (Exception ex)
        {
            return new TsxInsertResult(false, ex.Message, 0, 0);
        }
    }

    /// <summary>
    /// Inserts missing properties into all TSX files in a directory.
    /// </summary>
    internal static TsxDirectoryInsertResult InsertPropertiesInDirectory(
        string directoryPath,
        bool insertTileProperties = true,
        bool insertWangSetProperties = true)
    {
        var absoluteDir = ProjectSettings.GlobalizePath(directoryPath);
        if (!Directory.Exists(absoluteDir))
            return new TsxDirectoryInsertResult(false, $"Directory not found: {absoluteDir}", 0, 0, 0);

        var tsxFiles = Directory.GetFiles(absoluteDir, "*.tsx", SearchOption.AllDirectories);
        if (tsxFiles.Length == 0)
            return new TsxDirectoryInsertResult(true, "No TSX files found", 0, 0, 0);

        var filesModified = 0;
        var totalTiles = 0;
        var totalWangSets = 0;
        var errors = new List<string>();

        foreach (var tsxPath in tsxFiles)
        {
            var result = InsertProperties(tsxPath, insertTileProperties, insertWangSetProperties);
            if (!result.Success)
            {
                errors.Add($"{Path.GetFileName(tsxPath)}: {result.Message}");
                continue;
            }

            if (result.TilesModified > 0 || result.WangSetsModified > 0)
            {
                filesModified++;
                totalTiles += result.TilesModified;
                totalWangSets += result.WangSetsModified;
            }
        }

        var message = errors.Count > 0
            ? $"Errors: {string.Join("; ", errors)}"
            : "";

        return new TsxDirectoryInsertResult(errors.Count == 0, message, filesModified, totalTiles, totalWangSets);
    }

    private static int InsertIntoTiles(XElement tileset)
    {
        var tilesModified = 0;
        foreach (var tileElement in tileset.Elements("tile"))
        {
            var tileId = int.Parse(tileElement.Attribute("id")?.Value ?? "-1");
            if (tileId < 0)
                continue;

            if (TsxElementPropertyInserter.InsertIntoTile(tileElement))
                tilesModified++;
        }

        return tilesModified;
    }

    private static int InsertIntoWangSets(XElement tileset)
    {
        var wangSetsElement = tileset.Element("wangsets");
        if (wangSetsElement == null)
            return 0;

        var wangSetsModified = 0;
        foreach (var wangSetElement in wangSetsElement.Elements("wangset"))
        {
            if (TsxElementPropertyInserter.InsertIntoWangSet(wangSetElement))
                wangSetsModified++;
        }

        return wangSetsModified;
    }

    private static void SaveModified(XDocument doc, string tsxPath, TsxInsertResult result)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tsxPath);
        using var writer = new StreamWriter(absolutePath, false, new System.Text.UTF8Encoding(false));
        doc.Save(writer, SaveOptions.None);

        GD.Print(
            $"[TsxPropertyInserter] Updated {tsxPath}: " +
            $"{result.TilesModified} tiles, {result.WangSetsModified} wang sets");
    }
}
#endif
