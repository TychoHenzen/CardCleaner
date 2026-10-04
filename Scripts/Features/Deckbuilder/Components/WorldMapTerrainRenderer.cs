using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// Handles terrain rendering for the world map using dual-grid auto-tiling.
/// Manages tile rendering to appropriate TileMapLayers.
/// </summary>
public class WorldMapTerrainRenderer
{
    private readonly TileMapLayer? _terrainLayer;
    private readonly TileMapLayer? _decorationLayer;
    private readonly TileMapLayer? _structureLayer;
    private readonly TileMapLayer? _effectLayer;
    private readonly ITileRegistry? _tileRegistry;
    private readonly TileRenderInfoResolver _tileInfoResolver;
    private readonly TerrainTransitionRenderer _terrainTransitionRenderer;
    private readonly NonTerrainTileRenderer _nonTerrainTileRenderer;
    private bool _hasLoggedTileInfo;

    public WorldMapTerrainRenderer(
        TileMapLayer? terrainLayer,
        TileMapLayer? decorationLayer,
        TileMapLayer? structureLayer,
        TileMapLayer? effectLayer,
        ITileRegistry? tileRegistry,
        bool usingCompiledAtlas)
    {
        _terrainLayer = terrainLayer;
        _decorationLayer = decorationLayer;
        _structureLayer = structureLayer;
        _effectLayer = effectLayer;
        _tileRegistry = tileRegistry;
        _tileInfoResolver = new TileRenderInfoResolver(tileRegistry);
        _terrainTransitionRenderer = new TerrainTransitionRenderer(
            terrainLayer,
            tileRegistry,
            usingCompiledAtlas);
        _nonTerrainTileRenderer = new NonTerrainTileRenderer(
            decorationLayer,
            structureLayer,
            effectLayer,
            _tileInfoResolver);
    }

    /// <summary>
    /// Clear all tile layers.
    /// </summary>
    public void ClearAllLayers()
    {
        _terrainLayer?.Clear();
        _decorationLayer?.Clear();
        _structureLayer?.Clear();
        _effectLayer?.Clear();
    }

    /// <summary>
    /// Render dual-grid terrain transitions.
    /// Uses pre-composited transition tiles from the compiled atlas.
    /// </summary>
    public void RenderTerrainTransitions(SimpleMapData mapData)
    {
        _terrainTransitionRenderer.Render(mapData);
    }

    /// <summary>
    /// Render non-terrain tiles (structures, decorations, effects) from the data grid.
    /// </summary>
    public void RenderNonTerrainTiles(SimpleMapData mapData)
    {
        _nonTerrainTileRenderer.Render(mapData);
    }

    /// <summary>
    /// Get full render info for a tile including size for multi-tile support.
    /// </summary>
    public TileRenderInfo GetTileRenderInfoFull(string tileId)
    {
        return _tileInfoResolver.ResolveFull(tileId);
    }

    /// <summary>
    /// Get render info (sourceId, atlasCoords) for a tile.
    /// </summary>
    public TileRenderCoordinates GetTileRenderInfo(string tileId)
    {
        return _tileInfoResolver.Resolve(tileId);
    }

    /// <summary>
    /// Log sample of tile rendering info for debugging.
    /// </summary>
    public void LogTileRenderingSample(SimpleMapData mapData)
    {
        if (_hasLoggedTileInfo) return;
        _hasLoggedTileInfo = true;

        ILog.Print($"[TILE DEBUG] === Tile Rendering Debug ===");
        ILog.Print($"[TILE DEBUG] TileRegistry available: {_tileRegistry != null}");
        ILog.Print($"[TILE DEBUG] TerrainLayer available: {_terrainLayer != null}");

        if (_tileRegistry != null)
        {
            foreach (var tile in _tileRegistry.GetAllTiles())
            {
                ILog.Print(
                    $"[TILE DEBUG] Registered: id='{tile.Id}' sourceId={tile.SourceId} " +
                    $"atlas={tile.AtlasCoords} passable={tile.IsPassable}");
            }
        }

        // Log sample of actual map tiles
        var sampleCount = 0;
        for (var y = 0; y < mapData.Size.Y && sampleCount < 10; y++)
        for (var x = 0; x < mapData.Size.X && sampleCount < 10; x++)
        {
            var pos = new Vector2I(x, y);
            var tileId = mapData.GetTileId(pos);
            var (sourceId, atlasCoords) = GetTileRenderInfo(tileId);
            ILog.Print($"[TILE DEBUG] Map[{x},{y}] = '{tileId}' -> sourceId={sourceId} atlas={atlasCoords}");
            sampleCount++;
        }
    }
}
