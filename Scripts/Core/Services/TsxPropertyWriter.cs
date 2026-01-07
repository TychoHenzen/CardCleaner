using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Core.Services;

/// <summary>
/// Represents a typed property for TSX files.
/// </summary>
public class TsxProperty
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "string";
    public string Value { get; set; } = "";

    public TsxProperty() { }

    public TsxProperty(string name, string type, string value)
    {
        Name = name;
        Type = type;
        Value = value;
    }

    // Factory methods for type-safe property creation
    public static TsxProperty String(string name, string value) => new(name, "string", value);
    public static TsxProperty Int(string name, int value) => new(name, "int", value.ToString());
    public static TsxProperty Float(string name, float value) =>
        new(name, "float", value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    public static TsxProperty Bool(string name, bool value) => new(name, "bool", value.ToString().ToLowerInvariant());

    // Value accessors
    public string AsString() => Value;
    public int AsInt() => int.TryParse(Value, out var v) ? v : 0;
    public float AsFloat() => float.TryParse(Value, System.Globalization.NumberStyles.Float,
        System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0f;
    public bool AsBool() => Value.ToLowerInvariant() is "true" or "1";
}

/// <summary>
/// Provides write capabilities for TSX tileset files, allowing property modification
/// while preserving the original XML structure.
/// </summary>
public static class TsxPropertyWriter
{
    /// <summary>
    /// Modifies properties in a TSX file for specified tiles and/or wang sets.
    /// Preserves all other XML structure including wang colors, animations, and editor settings.
    /// </summary>
    /// <param name="tsxPath">Path to the TSX file (res:// or absolute)</param>
    /// <param name="tileProperties">Dictionary of tile ID → list of properties to set</param>
    /// <param name="wangSetProperties">Dictionary of wang set name → list of properties to set</param>
    /// <returns>Success status and message</returns>
    public static (bool success, string message) ModifyTsxProperties(
        string tsxPath,
        Dictionary<int, List<TsxProperty>>? tileProperties = null,
        Dictionary<string, List<TsxProperty>>? wangSetProperties = null)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tsxPath);
        if (!File.Exists(absolutePath))
        {
            return (false, $"TSX file not found: {absolutePath}");
        }

        try
        {
            var doc = XDocument.Load(absolutePath);
            var tileset = doc.Root;
            if (tileset == null || tileset.Name != "tileset")
            {
                return (false, "Invalid TSX: missing tileset root element");
            }

            var columns = ParseInt(tileset.Attribute("columns")?.Value, 1);

            // Modify tile properties
            if (tileProperties != null)
            {
                foreach (var (tileId, props) in tileProperties)
                {
                    ModifyTileProperties(tileset, tileId, props, columns);
                }
            }

            // Modify wang set properties
            if (wangSetProperties != null)
            {
                var wangsets = tileset.Element("wangsets");
                if (wangsets != null)
                {
                    foreach (var (wangSetName, props) in wangSetProperties)
                    {
                        ModifyWangSetProperties(wangsets, wangSetName, props);
                    }
                }
            }

            // Save with proper UTF8 encoding (no BOM)
            using var writer = new StreamWriter(absolutePath, false, new System.Text.UTF8Encoding(false));
            doc.Save(writer, SaveOptions.None);

            ILog.Print($"[TsxPropertyWriter] Updated properties in {tsxPath}");
            return (true, "Properties updated successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error modifying TSX: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a backup of a TSX file before modification.
    /// </summary>
    public static (bool success, string backupPath) CreateBackup(string tsxPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tsxPath);
        if (!File.Exists(absolutePath))
        {
            return (false, "");
        }

        try
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupPath = Path.ChangeExtension(absolutePath, $".backup_{timestamp}.tsx");
            File.Copy(absolutePath, backupPath, overwrite: false);
            ILog.Print($"[TsxPropertyWriter] Created backup: {backupPath}");
            return (true, backupPath);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TsxPropertyWriter] Backup failed: {ex.Message}");
            return (false, "");
        }
    }

    /// <summary>
    /// Reads all custom properties from a TSX file with type information preserved.
    /// </summary>
    public static (Dictionary<int, List<TsxProperty>> tileProperties, Dictionary<string, List<TsxProperty>> wangSetProperties)
        ReadTsxProperties(string tsxPath)
    {
        var tileProps = new Dictionary<int, List<TsxProperty>>();
        var wangSetProps = new Dictionary<string, List<TsxProperty>>();

        var absolutePath = ProjectSettings.GlobalizePath(tsxPath);
        if (!File.Exists(absolutePath))
        {
            return (tileProps, wangSetProps);
        }

        try
        {
            var doc = XDocument.Load(absolutePath);
            var tileset = doc.Root;
            if (tileset == null) return (tileProps, wangSetProps);

            // Read tile properties
            foreach (var tile in tileset.Elements("tile"))
            {
                var tileId = ParseInt(tile.Attribute("id")?.Value, -1);
                if (tileId >= 0)
                {
                    tileProps[tileId] = ParsePropertiesTyped(tile.Element("properties"));
                }
            }

            // Read wang set properties
            var wangsets = tileset.Element("wangsets");
            if (wangsets != null)
            {
                foreach (var wangset in wangsets.Elements("wangset"))
                {
                    var name = wangset.Attribute("name")?.Value;
                    if (!string.IsNullOrEmpty(name))
                    {
                        wangSetProps[name] = ParsePropertiesTyped(wangset.Element("properties"));
                    }
                }
            }

            return (tileProps, wangSetProps);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TsxPropertyWriter] Error reading TSX properties: {ex.Message}");
            return (tileProps, wangSetProps);
        }
    }

    /// <summary>
    /// Gets a list of all TSX files in a directory.
    /// </summary>
    public static List<string> FindTsxFiles(string directoryPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(directoryPath);
        if (!Directory.Exists(absolutePath))
        {
            return new List<string>();
        }

        return Directory.GetFiles(absolutePath, "*.tsx", SearchOption.AllDirectories)
            .ToList();
    }

    #region Private Implementation

    private static void ModifyTileProperties(XElement tileset, int tileId, List<TsxProperty> properties, int columns)
    {
        var tileElement = tileset.Elements("tile")
            .FirstOrDefault(t => ParseInt(t.Attribute("id")?.Value, -1) == tileId);

        if (tileElement == null)
        {
            // Create new tile element
            tileElement = new XElement("tile", new XAttribute("id", tileId));
            InsertTileInOrder(tileset, tileElement, tileId);
        }

        UpdatePropertiesElement(tileElement, properties);
    }

    private static void ModifyWangSetProperties(XElement wangsets, string wangSetName, List<TsxProperty> properties)
    {
        var wangset = wangsets.Elements("wangset")
            .FirstOrDefault(w => w.Attribute("name")?.Value == wangSetName);

        if (wangset == null)
        {
            ILog.Print($"[TsxPropertyWriter] Wang set '{wangSetName}' not found");
            return;
        }

        UpdatePropertiesElement(wangset, properties);
    }

    private static void UpdatePropertiesElement(XElement parent, List<TsxProperty> properties)
    {
        var propsElement = parent.Element("properties");

        if (properties.Count == 0)
        {
            propsElement?.Remove();
            return;
        }

        if (propsElement == null)
        {
            propsElement = new XElement("properties");
            // Insert properties element at the beginning of the parent
            parent.AddFirst(propsElement);
        }

        // Build a lookup of existing properties
        var existingProps = propsElement.Elements("property")
            .ToDictionary(
                p => p.Attribute("name")?.Value ?? "",
                p => p,
                StringComparer.OrdinalIgnoreCase);

        foreach (var prop in properties)
        {
            if (existingProps.TryGetValue(prop.Name, out var existingProp))
            {
                // Update existing property in place
                existingProp.SetAttributeValue("type", prop.Type);
                existingProp.SetAttributeValue("value", prop.Value);
            }
            else
            {
                // Add new property
                propsElement.Add(new XElement("property",
                    new XAttribute("name", prop.Name),
                    new XAttribute("type", prop.Type),
                    new XAttribute("value", prop.Value)));
            }
        }
    }

    private static void InsertTileInOrder(XElement tileset, XElement newTile, int tileId)
    {
        var tiles = tileset.Elements("tile").ToList();

        // Find insertion point to maintain tile ID order
        XElement? insertBefore = null;
        foreach (var tile in tiles)
        {
            var existingId = ParseInt(tile.Attribute("id")?.Value, int.MaxValue);
            if (tileId < existingId)
            {
                insertBefore = tile;
                break;
            }
        }

        if (insertBefore != null)
        {
            insertBefore.AddBeforeSelf(newTile);
        }
        else
        {
            // Insert before wangsets if present, otherwise at end
            var wangsets = tileset.Element("wangsets");
            if (wangsets != null)
                wangsets.AddBeforeSelf(newTile);
            else
                tileset.Add(newTile);
        }
    }

    private static List<TsxProperty> ParsePropertiesTyped(XElement? propsElement)
    {
        var result = new List<TsxProperty>();
        if (propsElement == null) return result;

        foreach (var prop in propsElement.Elements("property"))
        {
            var name = prop.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            var type = prop.Attribute("type")?.Value ?? "string";
            var value = prop.Attribute("value")?.Value ?? prop.Value;

            result.Add(new TsxProperty(name, type, value));
        }

        return result;
    }

    private static int ParseInt(string? value, int defaultValue)
        => int.TryParse(value, out var result) ? result : defaultValue;

    #endregion
}
