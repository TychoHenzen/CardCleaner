#if TOOLS
using System.Linq;
using System.Xml.Linq;

namespace CardCleaner.Addons.TileEditor;

/// <summary>
/// Inserts missing schema properties into a single tile or wang set element.
/// </summary>
internal static class TsxElementPropertyInserter
{
    /// <summary>
    /// Inserts missing properties into a tile element.
    /// Only tiles with a 'type' attribute (the tile ID) are named tiles that need properties.
    /// The display name is set explicitly per tile and never defaulted here.
    /// </summary>
    internal static bool InsertIntoTile(XElement tileElement)
    {
        var tileType = tileElement.Attribute("type")?.Value;
        if (string.IsNullOrEmpty(tileType))
            return false;

        var propsElement = tileElement.Element("properties");
        var existingNames = TsxSchemaPropertyWriter.ReadExistingNames(propsElement);

        var created = propsElement == null;
        if (propsElement == null)
        {
            propsElement = new XElement("properties");
            tileElement.AddFirst(propsElement);
        }

        var schema = TsxPropertySchema.TileProperties.Where(p => p.Name != "name");
        var added = TsxSchemaPropertyWriter.AppendMissing(propsElement, schema, existingNames);
        return created || added;
    }

    /// <summary>
    /// Inserts missing properties into a wang set element.
    /// </summary>
    internal static bool InsertIntoWangSet(XElement wangSetElement)
    {
        var propsElement = wangSetElement.Element("properties");
        var existingNames = TsxSchemaPropertyWriter.ReadExistingNames(propsElement);

        var created = propsElement == null;
        if (propsElement == null)
        {
            propsElement = new XElement("properties");
            AddAfterWangColors(wangSetElement, propsElement);
        }

        var added = TsxSchemaPropertyWriter.AppendMissing(
            propsElement, TsxPropertySchema.WangSetProperties, existingNames);
        return created || added;
    }

    private static void AddAfterWangColors(XElement wangSetElement, XElement propsElement)
    {
        var lastWangColor = wangSetElement.Elements("wangcolor").LastOrDefault();
        if (lastWangColor == null)
        {
            wangSetElement.AddFirst(propsElement);
            return;
        }

        lastWangColor.AddAfterSelf(propsElement);
    }
}
#endif
