using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Represents a chunk of tile data from a TMX infinite map.
/// </summary>
public class TmxChunk
{
    public Vector2I ChunkPosition { get; }
    public int Width { get; }
    public int Height { get; }
    private readonly int[] _tiles;

    public TmxChunk(Vector2I chunkPosition, int width, int height, int[] tiles)
    {
        ChunkPosition = chunkPosition;
        Width = width;
        Height = height;
        _tiles = tiles;
    }

    public int GetTileAt(int localX, int localY)
    {
        if (localX < 0 || localX >= Width || localY < 0 || localY >= Height)
            return 0;
        return _tiles[localY * Width + localX];
    }
}

/// <summary>
/// Reference to a tileset loaded from TMX, including tile definitions.
/// </summary>
public class TmxTilesetReference
{
    public int FirstGid { get; }
    public int LastGid { get; }
    public string TsxPath { get; }
    public TileRegistryResult TilesetData { get; }
    public int Columns { get; }
    private readonly Dictionary<int, TileDefinition> _tileCache;

    public TmxTilesetReference(int firstGid, int lastGid, string tsxPath, TileRegistryResult tilesetData, int columns)
    {
        FirstGid = firstGid;
        LastGid = lastGid;
        TsxPath = tsxPath;
        TilesetData = tilesetData;
        Columns = columns;
        // Pre-build lookup cache - key by atlas coords converted to local ID
        _tileCache = new Dictionary<int, TileDefinition>();
        foreach (var tile in tilesetData.Tiles)
        {
            // Convert atlas coords back to local tile ID
            var localId = tile.AtlasCoords.Y * columns + tile.AtlasCoords.X;
            _tileCache[localId] = tile;
        }
    }

    public bool ContainsGlobalId(int globalId) => globalId >= FirstGid && globalId <= LastGid;

    public int GlobalToLocal(int globalId) => globalId - FirstGid;

    public TileDefinition? GetTileDefinition(int localId) => _tileCache.GetValueOrDefault(localId);

    /// <summary>
    /// Converts a local tile ID to atlas coordinates.
    /// </summary>
    public Vector2I LocalIdToAtlasCoords(int localId)
    {
        if (Columns <= 0) return new Vector2I(localId, 0);
        return new Vector2I(localId % Columns, localId / Columns);
    }
}

/// <summary>
/// Result of resolving a tile at a specific map position.
/// </summary>
public record TmxTileResolution(
    int GlobalTileId,
    int LocalTileId,
    Vector2I AtlasCoords,
    TmxTilesetReference Tileset,
    TileDefinition? TileDefinition);

/// <summary>
/// Loaded TMX map data with chunk-based tile storage.
/// </summary>
public class TmxMapData
{
    public Vector2I MapSize { get; }
    public List<TmxTilesetReference> Tilesets { get; }
    public int ChunkSize { get; }
    private readonly Dictionary<Vector2I, TmxChunk> _chunks;

    public TmxMapData(
        Vector2I mapSize,
        List<TmxTilesetReference> tilesets,
        Dictionary<Vector2I, TmxChunk> chunks,
        int chunkSize = 16)
    {
        MapSize = mapSize;
        Tilesets = tilesets;
        _chunks = chunks;
        ChunkSize = chunkSize;
    }

    /// <summary>
    /// Gets tile information at the specified world coordinates.
    /// </summary>
    public TmxTileResolution? GetTileAt(int x, int y)
    {
        // Calculate chunk coordinates (handle negative coords for infinite maps)
        var chunkX = x >= 0 ? x / ChunkSize : (x - ChunkSize + 1) / ChunkSize;
        var chunkY = y >= 0 ? y / ChunkSize : (y - ChunkSize + 1) / ChunkSize;
        var chunkCoord = new Vector2I(chunkX, chunkY);

        if (!_chunks.TryGetValue(chunkCoord, out var chunk))
            return null;

        // Calculate local position within chunk
        var localX = x - (chunkX * ChunkSize);
        var localY = y - (chunkY * ChunkSize);
        var globalId = chunk.GetTileAt(localX, localY);

        if (globalId == 0)
            return null;

        // Find which tileset contains this global ID
        foreach (var tileset in Tilesets)
        {
            if (tileset.ContainsGlobalId(globalId))
            {
                var localId = tileset.GlobalToLocal(globalId);
                var atlasCoords = tileset.LocalIdToAtlasCoords(localId);
                var tileDef = tileset.GetTileDefinition(localId);
                return new TmxTileResolution(globalId, localId, atlasCoords, tileset, tileDef);
            }
        }

        return null;
    }

    /// <summary>
    /// Gets all non-empty tiles in the map as coordinate-resolution pairs.
    /// </summary>
    public IEnumerable<(Vector2I Coord, TmxTileResolution Resolution)> GetAllTiles()
    {
        foreach (var (chunkCoord, chunk) in _chunks)
        {
            for (var ly = 0; ly < chunk.Height; ly++)
            for (var lx = 0; lx < chunk.Width; lx++)
            {
                var worldX = chunkCoord.X * ChunkSize + lx;
                var worldY = chunkCoord.Y * ChunkSize + ly;
                var resolution = GetTileAt(worldX, worldY);
                if (resolution != null)
                    yield return (new Vector2I(worldX, worldY), resolution);
            }
        }
    }

    /// <summary>
    /// Gets the bounds of the map (min/max coordinates with tiles).
    /// </summary>
    public (Vector2I Min, Vector2I Max) GetBounds()
    {
        if (_chunks.Count == 0)
            return (Vector2I.Zero, Vector2I.Zero);

        var minX = int.MaxValue;
        var minY = int.MaxValue;
        var maxX = int.MinValue;
        var maxY = int.MinValue;

        foreach (var chunkCoord in _chunks.Keys)
        {
            minX = Math.Min(minX, chunkCoord.X * ChunkSize);
            minY = Math.Min(minY, chunkCoord.Y * ChunkSize);
            maxX = Math.Max(maxX, (chunkCoord.X + 1) * ChunkSize - 1);
            maxY = Math.Max(maxY, (chunkCoord.Y + 1) * ChunkSize - 1);
        }

        return (new Vector2I(minX, minY), new Vector2I(maxX, maxY));
    }
}

/// <summary>
/// Loads TMX map files with full tile resolution support.
/// </summary>
public static class TmxMapLoader
{
    /// <summary>
    /// Loads a TMX map file with full tile resolution support.
    /// </summary>
    public static TmxMapData? LoadTmxMap(string tmxPath)
    {
        var absolutePath = ProjectSettings.GlobalizePath(tmxPath);
        if (!File.Exists(absolutePath))
        {
            ILog.Print($"[TmxMapLoader] TMX file not found: {absolutePath}");
            return null;
        }

        try
        {
            var doc = XDocument.Load(absolutePath);
            var map = doc.Root;
            if (map == null || map.Name != "map")
            {
                ILog.Print("[TmxMapLoader] Invalid TMX: missing map root element");
                return null;
            }

            var tmxDir = Path.GetDirectoryName(absolutePath) ?? "";
            var mapWidth = ParseHelpers.ParseInt(map.Attribute("width")?.Value, 0);
            var mapHeight = ParseHelpers.ParseInt(map.Attribute("height")?.Value, 0);

            // Load tileset references
            var tilesets = new List<TmxTilesetReference>();
            foreach (var tilesetRef in map.Elements("tileset"))
            {
                var firstGid = ParseHelpers.ParseInt(tilesetRef.Attribute("firstgid")?.Value, 1);
                var source = tilesetRef.Attribute("source")?.Value;

                // Skip internal/builtin tilesets (e.g., ":/" paths)
                if (string.IsNullOrEmpty(source) || source.StartsWith(":/"))
                    continue;

                var tsxAbsolutePath = Path.GetFullPath(Path.Combine(tmxDir, source));
                if (!File.Exists(tsxAbsolutePath))
                {
                    ILog.Print($"[TmxMapLoader] TSX file not found: {tsxAbsolutePath}");
                    continue;
                }

                // Load TSX to get tile count and columns
                var tsxDoc = XDocument.Load(tsxAbsolutePath);
                var tileset = tsxDoc.Root;
                var tileCount = ParseHelpers.ParseInt(tileset?.Attribute("tilecount")?.Value, 0);
                var columns = ParseHelpers.ParseInt(tileset?.Attribute("columns")?.Value, 1);
                var lastGid = firstGid + tileCount - 1;

                // Load tile definitions
                var tilesetData = TiledTilesetLoader.LoadFromTsx(tsxAbsolutePath, 0);

                tilesets.Add(new TmxTilesetReference(firstGid, lastGid, tsxAbsolutePath, tilesetData, columns));
            }

            // Load layer chunks
            var chunks = new Dictionary<Vector2I, TmxChunk>();
            foreach (var layer in map.Elements("layer"))
            {
                var data = layer.Element("data");
                if (data == null) continue;

                foreach (var chunkElement in data.Elements("chunk"))
                {
                    var chunkX = ParseHelpers.ParseInt(chunkElement.Attribute("x")?.Value, 0);
                    var chunkY = ParseHelpers.ParseInt(chunkElement.Attribute("y")?.Value, 0);
                    var chunkWidth = ParseHelpers.ParseInt(chunkElement.Attribute("width")?.Value, 16);
                    var chunkHeight = ParseHelpers.ParseInt(chunkElement.Attribute("height")?.Value, 16);

                    var csv = chunkElement.Value.Trim();
                    var tiles = csv.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => ParseHelpers.ParseInt(s.Trim(), 0))
                        .ToArray();

                    // Chunk coord is based on tile position divided by chunk size
                    var chunkCoord = new Vector2I(chunkX / chunkWidth, chunkY / chunkHeight);
                    chunks[chunkCoord] = new TmxChunk(chunkCoord, chunkWidth, chunkHeight, tiles);
                }
            }

            ILog.Print($"[TmxMapLoader] Loaded TMX map: {chunks.Count} chunks, {tilesets.Count} tilesets");
            return new TmxMapData(new Vector2I(mapWidth, mapHeight), tilesets, chunks);
        }
        catch (Exception ex)
        {
            ILog.Print($"[TmxMapLoader] Error loading TMX map: {ex.Message}");
            return null;
        }
    }
}
