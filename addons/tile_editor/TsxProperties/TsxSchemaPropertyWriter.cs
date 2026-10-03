#if TOOLS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Services;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Reads existing property names from a TSX properties element and appends schema
/// properties that are not present yet.
/// </summary>
internal static class TsxSchemaPropertyWriter
{
    /// <summary>
    /// Returns the case-insensitive set of non-empty property names under a properties element.
    /// </summary>
    internal static HashSet<string> ReadExistingNames(XElement? propsElement)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (propsElement == null)
            return names;

        foreach (var prop in propsElement.Elements("property"))
        {
            var name = prop.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        return names;
    }

    /// <summary>
    /// Returns the schema property names that are missing from a properties element.
    /// </summary>
    internal static IEnumerable<string> FindMissingNames(
        XElement? propsElement,
        IEnumerable<TsxProperty> schema)
    {
        var existing = ReadExistingNames(propsElement);
        return schema.Where(p => !existing.Contains(p.Name)).Select(p => p.Name);
    }

    /// <summary>
    /// Appends every schema property whose name is not in <paramref name="existingNames"/>.
    /// </summary>
    /// <returns>True when at least one property element was added.</returns>
    internal static bool AppendMissing(
        XElement propsElement,
        IEnumerable<TsxProperty> schema,
        HashSet<string> existingNames)
    {
        var modified = false;
        foreach (var schemaProp in schema)
        {
            if (existingNames.Contains(schemaProp.Name))
                continue;

            propsElement.Add(CreatePropertyElement(schemaProp));
            modified = true;
        }

        return modified;
    }

    private static XElement CreatePropertyElement(TsxProperty schemaProp)
    {
        var propElement = new XElement("property",
            new XAttribute("name", schemaProp.Name),
            new XAttribute("type", schemaProp.Type));

        // Add propertytype for custom enum types
        if (!string.IsNullOrEmpty(schemaProp.PropertyType))
            propElement.Add(new XAttribute("propertytype", schemaProp.PropertyType));

        propElement.Add(new XAttribute("value", schemaProp.Value));
        return propElement;
    }
}
#endif
