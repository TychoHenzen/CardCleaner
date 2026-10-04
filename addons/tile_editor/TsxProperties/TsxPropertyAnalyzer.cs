#if TOOLS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Generates a report of missing properties in TSX files.
/// Useful for previewing what the inserter would do.
/// </summary>
internal static class TsxPropertyAnalyzer
{
    internal static List<TsxPropertyReport> AnalyzeDirectory(string directoryPath)
    {
        var reports = new List<TsxPropertyReport>();
        var absoluteDir = ProjectSettings.GlobalizePath(directoryPath);

        if (!Directory.Exists(absoluteDir))
            return reports;

        var tsxFiles = Directory.GetFiles(absoluteDir, "*.tsx", SearchOption.AllDirectories);

        foreach (var tsxPath in tsxFiles)
        {
            var report = AnalyzeFile(tsxPath);
            if (report != null)
                reports.Add(report);
        }

        return reports;
    }

    /// <summary>
    /// Analyzes a single TSX file for missing properties.
    /// </summary>
    private static TsxPropertyReport? AnalyzeFile(string tsxPath)
    {
        try
        {
            var tileset = XDocument.Load(tsxPath).Root;
            if (tileset == null)
                return null;

            var report = new TsxPropertyReport
            {
                TsxPath = tsxPath,
                FileName = Path.GetFileName(tsxPath)
            };

            CountTiles(tileset, report);
            CountWangSets(tileset, report);
            return report;
        }
        catch
        {
            return null;
        }
    }

    // Tiles with a type attribute (which serves as the tile ID) are the named tiles
    private static void CountTiles(XElement tileset, TsxPropertyReport report)
    {
        var namedTiles = tileset.Elements("tile")
            .Where(tile => !string.IsNullOrEmpty(tile.Attribute("type")?.Value));
        var schema = TsxPropertySchema.TileProperties.Where(p => p.Name != "name").ToList();

        foreach (var tileElement in namedTiles)
        {
            report.TilesWithId++;
            var missing = TsxSchemaPropertyWriter.FindMissingNames(
                tileElement.Element("properties"), schema);
            report.MissingTileProperties.UnionWith(missing);
        }
    }

    private static void CountWangSets(XElement tileset, TsxPropertyReport report)
    {
        var wangSets = tileset.Element("wangsets");
        if (wangSets == null)
            return;

        foreach (var wangSet in wangSets.Elements("wangset"))
        {
            report.WangSetCount++;
            var missing = TsxSchemaPropertyWriter.FindMissingNames(
                wangSet.Element("properties"), TsxPropertySchema.WangSetProperties);
            report.MissingWangSetProperties.UnionWith(missing);
        }
    }
}
#endif
