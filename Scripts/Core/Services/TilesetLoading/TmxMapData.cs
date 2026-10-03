using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services.TilesetLoading.TmxMapping;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Loaded TMX map data with chunk-based tile storage.
/// </summary>
internal sealed class TmxMapData
{
    internal Vector2I MapSize { get; }
    internal List<TmxTilesetReference> Tilesets { get; }
    internal int ChunkSize { get; }
    private readonly Dictionary<Vector2I, TmxChunk> _chunks;

    internal TmxMapData(
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
    internal TmxTileResolution? GetTileAt(int x, int y)
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
    internal IEnumerable<(Vector2I Coord, TmxTileResolution Resolution)> GetAllTiles()
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
    internal TmxBounds GetBounds()
    {
        if (_chunks.Count == 0)
            return new TmxBounds(Vector2I.Zero, Vector2I.Zero);

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

        return new TmxBounds(new Vector2I(minX, minY), new Vector2I(maxX, maxY));
    }
}
