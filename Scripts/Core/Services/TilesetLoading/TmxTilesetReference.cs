using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Core.Services.TilesetLoading;

/// <summary>
/// Reference to a tileset loaded from TMX, including tile definitions.
/// </summary>
internal sealed class TmxTilesetReference
{
    internal int FirstGid { get; }
    internal int LastGid { get; }
    internal string TsxPath { get; }
    internal TileRegistryResult TilesetData { get; }
    internal int Columns { get; }
    private readonly Dictionary<int, TileDefinition> _tileCache;

    internal TmxTilesetReference(int firstGid, int lastGid, string tsxPath, TileRegistryResult tilesetData, int columns)
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

    internal bool ContainsGlobalId(int globalId) => globalId >= FirstGid && globalId <= LastGid;

    internal int GlobalToLocal(int globalId) => globalId - FirstGid;

    internal TileDefinition? GetTileDefinition(int localId) => _tileCache.GetValueOrDefault(localId);

    /// <summary>
    /// Converts a local tile ID to atlas coordinates.
    /// </summary>
    internal Vector2I LocalIdToAtlasCoords(int localId)
    {
        if (Columns <= 0) return new Vector2I(localId, 0);
        return new Vector2I(localId % Columns, localId / Columns);
    }
}
