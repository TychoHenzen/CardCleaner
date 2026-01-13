using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Parses tile properties from Tiled TSX tileset files.
/// Handles custom properties, animations, and type attributes.
/// </summary>
internal static class TilePropertyParser
{
    /// <summary>
    /// Parse custom properties for all tiles that have them.
    /// Also captures the 'type' attribute which serves as the tile's ID.
    /// </summary>
    public static Dictionary<int, TilePropertyData> ParseAllTileProperties(XElement tileset)
    {
        var result = new Dictionary<int, TilePropertyData>();

        foreach (var tile in tileset.Elements("tile"))
        {
            var tileId = ParseHelpers.ParseInt(tile.Attribute("id")?.Value, -1);
            if (tileId < 0) continue;

            var tileType = tile.Attribute("type")?.Value; // 'type' attr serves as the tile ID
            var props = ParseProperties(tile.Element("properties"));
            var animation = ParseAnimation(tile.Element("animation"));

            result[tileId] = new TilePropertyData(tileType, props, animation);
        }

        return result;
    }

    /// <summary>
    /// Parse a properties element into a dictionary.
    /// </summary>
    public static Dictionary<string, string> ParseProperties(XElement? propsElement)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (propsElement == null) return result;

        foreach (var prop in propsElement.Elements("property"))
        {
            var name = prop.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name)) continue;

            // Value can be in attribute or element content
            var value = prop.Attribute("value")?.Value ?? prop.Value;
            result[name] = value;
        }

        return result;
    }

    /// <summary>
    /// Parse Tiled's native animation element.
    /// </summary>
    private static List<AnimationFrame>? ParseAnimation(XElement? animElement)
    {
        if (animElement == null) return null;

        var frames = new List<AnimationFrame>();
        foreach (var frame in animElement.Elements("frame"))
        {
            var tileId = ParseHelpers.ParseInt(frame.Attribute("tileid")?.Value, -1);
            var duration = ParseHelpers.ParseInt(frame.Attribute("duration")?.Value, 100); // ms

            if (tileId >= 0)
                frames.Add(new AnimationFrame(tileId, duration / 1000f)); // Convert to seconds
        }

        return frames.Count > 0 ? frames : null;
    }
}

/// <summary>
/// Container for parsed tile properties.
/// </summary>
internal record TilePropertyData(string? Type, Dictionary<string, string> Properties, List<AnimationFrame>? Animation);

/// <summary>
/// Animation frame data.
/// </summary>
internal record AnimationFrame(int TileId, float Duration);
