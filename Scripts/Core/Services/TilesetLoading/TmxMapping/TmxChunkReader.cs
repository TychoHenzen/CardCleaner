using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;

/// <summary>
/// Reads the CSV tile chunks of a TMX infinite map.
/// </summary>
internal static class TmxChunkReader
{
    private const int DefaultChunkExtent = 16;
    private static readonly char[] CsvSeparators = [',', '\n', '\r'];

    internal static Dictionary<Vector2I, TmxChunk> ReadChunks(XElement map)
    {
        var chunks = new Dictionary<Vector2I, TmxChunk>();
        foreach (var chunkElement in map.Elements("layer").SelectMany(ChunkElements))
        {
            var chunk = ReadChunk(chunkElement);
            chunks[chunk.ChunkPosition] = chunk;
        }

        return chunks;
    }

    private static IEnumerable<XElement> ChunkElements(XElement layer)
        => layer.Element("data")?.Elements("chunk") ?? [];

    private static TmxChunk ReadChunk(XElement chunkElement)
    {
        var chunkX = ParseHelpers.ParseInt(chunkElement.Attribute("x")?.Value, 0);
        var chunkY = ParseHelpers.ParseInt(chunkElement.Attribute("y")?.Value, 0);
        var chunkWidth = ParseHelpers.ParseInt(chunkElement.Attribute("width")?.Value, DefaultChunkExtent);
        var chunkHeight = ParseHelpers.ParseInt(chunkElement.Attribute("height")?.Value, DefaultChunkExtent);

        var tiles = chunkElement.Value.Trim()
            .Split(CsvSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => ParseHelpers.ParseInt(s.Trim(), 0))
            .ToArray();

        // Chunk coord is based on tile position divided by chunk size
        var chunkCoord = new Vector2I(chunkX / chunkWidth, chunkY / chunkHeight);
        return new TmxChunk(chunkCoord, chunkWidth, chunkHeight, tiles);
    }
}
