#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Inserts required custom properties into TSX tileset files with sensible defaults.
/// Preserves existing property values.
/// </summary>
public static class TsxPropertyInserter
{
    /// <summary>
    /// Inserts missing required properties into a single TSX file.
    /// </summary>
    /// <param name="tsxPath">Path to the TSX file</param>
    /// <param name="insertTileProperties">Whether to insert tile properties (for tiles with id property)</param>
    /// <param name="insertWangSetProperties">Whether to insert wang set properties</param>
    /// <returns>Result with counts of modified tiles/wangsets</returns>
    public static (bool success, string message, int tilesModified, int wangSetsModified) InsertProperties(
        string tsxPath,
        bool insertTileProperties = true,
        bool insertWangSetProperties = true)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tsxPath);
        if (!File.Exists(absolutePath))
        {
            return (false, $"TSX file not found: {absolutePath}", 0, 0);
        }

        try
        {
            var doc = XDocument.Load(absolutePath);
            var tileset = doc.Root;
            if (tileset == null || tileset.Name != "tileset")
            {
                return (false, "Invalid TSX: missing tileset root", 0, 0);
            }

            var columns = int.Parse(tileset.Attribute("columns")?.Value ?? "1");
            var tilesModified = 0;
            var wangSetsModified = 0;

            // Insert properties for tiles
            if (insertTileProperties)
            {
                foreach (var tileElement in tileset.Elements("tile"))
                {
                    var tileId = int.Parse(tileElement.Attribute("id")?.Value ?? "-1");
                    if (tileId < 0) continue;

                    var modified = InsertMissingTileProperties(tileElement, tileId, columns);
                    if (modified)
                        tilesModified++;
                }
            }

            // Insert properties for wang sets
            if (insertWangSetProperties)
            {
                var wangSetsElement = tileset.Element("wangsets");
                if (wangSetsElement != null)
                {
                    foreach (var wangSetElement in wangSetsElement.Elements("wangset"))
                    {
                        var modified = InsertMissingWangSetProperties(wangSetElement);
                        if (modified)
                            wangSetsModified++;
                    }
                }
            }

            // Only save if modifications were made
            if (tilesModified > 0 || wangSetsModified > 0)
            {
                using var writer = new StreamWriter(absolutePath, false, new System.Text.UTF8Encoding(false));
                doc.Save(writer, SaveOptions.None);

                GD.Print($"[TsxPropertyInserter] Updated {tsxPath}: {tilesModified} tiles, {wangSetsModified} wang sets");
            }

            return (true, "", tilesModified, wangSetsModified);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, 0, 0);
        }
    }

    /// <summary>
    /// Inserts missing properties into all TSX files in a directory.
    /// </summary>
    public static (bool success, string message, int filesModified, int totalTiles, int totalWangSets) InsertPropertiesInDirectory(
        string directoryPath,
        bool insertTileProperties = true,
        bool insertWangSetProperties = true)
    {
        var absoluteDir = ProjectSettings.GlobalizePath(directoryPath);
        if (!Directory.Exists(absoluteDir))
        {
            return (false, $"Directory not found: {absoluteDir}", 0, 0, 0);
        }

        var tsxFiles = Directory.GetFiles(absoluteDir, "*.tsx", SearchOption.AllDirectories);
        if (tsxFiles.Length == 0)
        {
            return (true, "No TSX files found", 0, 0, 0);
        }

        var filesModified = 0;
        var totalTiles = 0;
        var totalWangSets = 0;
        var errors = new List<string>();

        foreach (var tsxPath in tsxFiles)
        {
            var result = InsertProperties(tsxPath, insertTileProperties, insertWangSetProperties);
            if (!result.success)
            {
                errors.Add($"{Path.GetFileName(tsxPath)}: {result.message}");
                continue;
            }

            if (result.tilesModified > 0 || result.wangSetsModified > 0)
            {
                filesModified++;
                totalTiles += result.tilesModified;
                totalWangSets += result.wangSetsModified;
            }
        }

        var message = errors.Count > 0
            ? $"Errors: {string.Join("; ", errors)}"
            : "";

        return (errors.Count == 0, message, filesModified, totalTiles, totalWangSets);
    }

    /// <summary>
    /// Inserts missing properties into a tile element.
    /// </summary>
    private static bool InsertMissingTileProperties(XElement tileElement, int tileId, int columns)
    {
        // Only add properties to tiles that have a 'type' attribute (which serves as the tile ID)
        // Tiles without a type are not named tiles and don't need properties
        var tileType = tileElement.Attribute("type")?.Value;
        if (string.IsNullOrEmpty(tileType))
        {
            return false;
        }

        var propsElement = tileElement.Element("properties");
        var existingProps = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

        if (propsElement != null)
        {
            foreach (var prop in propsElement.Elements("property"))
            {
                var name = prop.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    existingProps[name] = prop;
                }
            }
        }

        var modified = false;

        // Create properties element if it doesn't exist
        if (propsElement == null)
        {
            propsElement = new XElement("properties");
            tileElement.AddFirst(propsElement);
            modified = true;
        }

        // Insert missing core properties
        foreach (var schemaProp in TsxPropertySchema.TileProperties)
        {
            // Skip name - it should be set explicitly per tile
            if (schemaProp.Name == "name")
                continue;

            if (!existingProps.ContainsKey(schemaProp.Name))
            {
                var propElement = new XElement("property",
                    new XAttribute("name", schemaProp.Name),
                    new XAttribute("type", schemaProp.Type));

                // Add propertytype for custom enum types
                if (!string.IsNullOrEmpty(schemaProp.PropertyType))
                {
                    propElement.Add(new XAttribute("propertytype", schemaProp.PropertyType));
                }

                propElement.Add(new XAttribute("value", schemaProp.Value));
                propsElement.Add(propElement);
                modified = true;
            }
        }

        return modified;
    }

    /// <summary>
    /// Inserts missing properties into a wang set element.
    /// </summary>
    private static bool InsertMissingWangSetProperties(XElement wangSetElement)
    {
        var propsElement = wangSetElement.Element("properties");
        var existingProps = new Dictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

        if (propsElement != null)
        {
            foreach (var prop in propsElement.Elements("property"))
            {
                var name = prop.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                {
                    existingProps[name] = prop;
                }
            }
        }

        var modified = false;

        // Create properties element if it doesn't exist
        if (propsElement == null)
        {
            propsElement = new XElement("properties");

            // Insert after wangcolor elements
            var lastWangColor = wangSetElement.Elements("wangcolor").LastOrDefault();
            if (lastWangColor != null)
            {
                lastWangColor.AddAfterSelf(propsElement);
            }
            else
            {
                wangSetElement.AddFirst(propsElement);
            }
            modified = true;
        }

        // Insert missing wang set properties
        foreach (var schemaProp in TsxPropertySchema.WangSetProperties)
        {
            if (!existingProps.ContainsKey(schemaProp.Name))
            {
                var propElement = new XElement("property",
                    new XAttribute("name", schemaProp.Name),
                    new XAttribute("type", schemaProp.Type));

                // Add propertytype for custom enum types
                if (!string.IsNullOrEmpty(schemaProp.PropertyType))
                {
                    propElement.Add(new XAttribute("propertytype", schemaProp.PropertyType));
                }

                propElement.Add(new XAttribute("value", schemaProp.Value));
                propsElement.Add(propElement);
                modified = true;
            }
        }

        return modified;
    }

    /// <summary>
    /// Generates a report of missing properties in TSX files.
    /// Useful for previewing what InsertProperties would do.
    /// </summary>
    public static List<TsxPropertyReport> AnalyzePropertiesInDirectory(string directoryPath)
    {
        var reports = new List<TsxPropertyReport>();
        var absoluteDir = ProjectSettings.GlobalizePath(directoryPath);

        if (!Directory.Exists(absoluteDir))
            return reports;

        var tsxFiles = Directory.GetFiles(absoluteDir, "*.tsx", SearchOption.AllDirectories);

        foreach (var tsxPath in tsxFiles)
        {
            var report = AnalyzeTsxFile(tsxPath);
            if (report != null)
                reports.Add(report);
        }

        return reports;
    }

    /// <summary>
    /// Analyzes a single TSX file for missing properties.
    /// </summary>
    private static TsxPropertyReport? AnalyzeTsxFile(string tsxPath)
    {
        try
        {
            var doc = XDocument.Load(tsxPath);
            var tileset = doc.Root;
            if (tileset == null) return null;

            var report = new TsxPropertyReport
            {
                TsxPath = tsxPath,
                FileName = Path.GetFileName(tsxPath)
            };

            // Count tiles with type attribute (which serves as the tile ID)
            foreach (var tileElement in tileset.Elements("tile"))
            {
                var tileType = tileElement.Attribute("type")?.Value;
                if (string.IsNullOrEmpty(tileType))
                    continue;

                report.TilesWithId++;
                var props = tileElement.Element("properties");
                var existingNames = new HashSet<string>(
                    props?.Elements("property")
                        .Select(p => p.Attribute("name")?.Value ?? "")
                        .Where(n => !string.IsNullOrEmpty(n)) ?? Array.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var schemaProp in TsxPropertySchema.TileProperties)
                {
                    if (schemaProp.Name != "name" && !existingNames.Contains(schemaProp.Name))
                    {
                        report.MissingTileProperties.Add(schemaProp.Name);
                    }
                }
            }

            // Count wang sets
            var wangSets = tileset.Element("wangsets");
            if (wangSets != null)
            {
                foreach (var wangSet in wangSets.Elements("wangset"))
                {
                    report.WangSetCount++;
                    var props = wangSet.Element("properties");
                    var existingNames = new HashSet<string>(
                        props?.Elements("property")
                            .Select(p => p.Attribute("name")?.Value ?? "")
                            .Where(n => !string.IsNullOrEmpty(n)) ?? Array.Empty<string>(),
                        StringComparer.OrdinalIgnoreCase);

                    foreach (var schemaProp in TsxPropertySchema.WangSetProperties)
                    {
                        if (!existingNames.Contains(schemaProp.Name))
                        {
                            report.MissingWangSetProperties.Add(schemaProp.Name);
                        }
                    }
                }
            }

            return report;
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Report of properties status in a TSX file.
/// </summary>
public class TsxPropertyReport
{
    public string TsxPath { get; set; } = "";
    public string FileName { get; set; } = "";
    public int TilesWithId { get; set; }
    public int WangSetCount { get; set; }
    public HashSet<string> MissingTileProperties { get; set; } = new();
    public HashSet<string> MissingWangSetProperties { get; set; } = new();

    public bool HasMissingProperties =>
        MissingTileProperties.Count > 0 || MissingWangSetProperties.Count > 0;
}
#endif
