#if TOOLS
using System.IO;
using System.Xml.Linq;
using Godot;
using static CardCleaner.Addons.TileEditor.TmxAtlasCompilerModels;

namespace CardCleaner.Addons.TileEditor;

internal static class TmxTsxSourceReader
{
    internal static TsxSourceReadResult Read(
        XElement tileset,
        string tsxPath,
        int targetTileSize)
    {
        var tileWidth = GetIntAttribute(tileset, "tilewidth", 16);
        var tileHeight = GetIntAttribute(tileset, "tileheight", 16);
        var columns = GetIntAttribute(tileset, "columns", 1);
        var imageElement = tileset.Element("image");
        if (imageElement == null)
            return Failure("TSX has no image element");

        var imageSource = GetAttributeValue(imageElement, "source");
        if (string.IsNullOrEmpty(imageSource))
            return Failure("TSX image has no source");

        var imagePath = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(tsxPath) ?? "",
            imageSource));
        if (!File.Exists(imagePath))
            return Failure($"Image not found: {imagePath}");

        var image = Image.LoadFromFile(imagePath);
        if (image == null)
            return Failure($"Failed to load image: {imagePath}");

        return new TsxSourceReadResult
        {
            Source = new TsxSourceInfo
            {
                TileWidth = tileWidth,
                TileHeight = tileHeight,
                Columns = columns,
                SourceScale = (float)targetTileSize / tileWidth,
                Image = image
            }
        };
    }

    private static int GetIntAttribute(XElement element, string name, int defaultValue)
        => int.Parse(GetAttributeValue(element, name, defaultValue.ToString()));

    private static string GetAttributeValue(
        XElement element,
        string name,
        string defaultValue = "")
        => element.Attribute(name)?.Value ?? defaultValue;

    private static TsxSourceReadResult Failure(string error)
        => new() { Error = error };
}
#endif
