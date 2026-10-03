using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
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
    private readonly ITransitionResolver? _transitionResolver;
    private readonly bool _usingCompiledAtlas;
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
        _usingCompiledAtlas = usingCompiledAtlas;

        // Only use transition resolver if compiled atlas is available
        if (_usingCompiledAtlas)
        {
            _transitionResolver = new CompiledTransitionResolver();
        }
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
        if (_terrainLayer == null || _tileRegistry == null)
            return;

        if (mapData.DecorationOverlays.Count == 0)
        {
            ILog.Print("[DUAL-GRID] No terrain data available, skipping dual-grid rendering");
            return;
        }

        // NO layer offset - visual grid renders at standard positions
        _terrainLayer.Position = Vector2.Zero;

        var tilesRendered = 0;
        var transitionsResolved = 0;
        var missingTileIds = new HashSet<string>();

        foreach (var (position, (baseTileId, topTileId, bitmask)) in mapData.DecorationOverlays)
        {
            var baseTile = _tileRegistry.GetTile(baseTileId);
            var topTile = _tileRegistry.GetTile(topTileId);

            if (baseTile == null)
            {
                missingTileIds.Add(baseTileId);
                continue;
            }

            var (sourceId, atlasCoords) = ResolveTransitionCoords(
                baseTile, topTile, baseTileId, topTileId, bitmask, ref transitionsResolved);

            // Get variant definition for offset and multi-cell support
            var variantTile = topTile ?? baseTile;
            var variant = variantTile.GetVariantDefinition(bitmask);

            if (variant.HasValue && variant.Value.HasOffset)
            {
                // Apply offset for multi-cell variants (e.g., tall trees)
                var renderPos = position + variant.Value.Offset;
                _terrainLayer.SetCell(renderPos, sourceId, atlasCoords);
            }
            else
            {
                // Standard single-cell rendering
                _terrainLayer.SetCell(position, sourceId, atlasCoords);
            }
            tilesRendered++;
        }

        var atlasStatus = _usingCompiledAtlas ? "compiled atlas" : "FALLBACK (no compiled atlas)";
        ILog.Print(
            $"[DUAL-GRID] Rendered {tilesRendered} terrain tiles ({transitionsResolved} transitions) " +
            $"using {atlasStatus}");

        if (missingTileIds.Count > 0)
            ILog.Print($"[DUAL-GRID] WARNING: Missing tiles: {string.Join(", ", missingTileIds)}");
    }

    private (int sourceId, Vector2I atlasCoords) ResolveTransitionCoords(
        TileDefinition baseTile,
        TileDefinition? topTile,
        string baseTileId,
        string topTileId,
        int bitmask,
        ref int transitionsResolved)
    {
        int sourceId;
        Vector2I atlasCoords;

        // If compiled atlas is not available, always use tile's own coordinates
        if (!_usingCompiledAtlas || _transitionResolver == null)
        {
            var effectiveTile = topTile ?? baseTile;
            atlasCoords = effectiveTile.HasAutoTileVariants
                ? effectiveTile.GetAutoTileCoords(bitmask)
                : effectiveTile.AtlasCoords;
            sourceId = effectiveTile.SourceId;
        }
        // For bitmask 0 (no foreground corners), render the base tile from compiled atlas
        else if (bitmask == 0)
        {
            var solidFillCoords = _transitionResolver.ResolveSolidFill(baseTileId);
            if (solidFillCoords.HasValue)
            {
                atlasCoords = solidFillCoords.Value;
                sourceId = _transitionResolver.CompiledAtlasSourceId;
            }
            else
            {
                // Base tile not in any transition - translate original coords to compiled atlas
                (sourceId, atlasCoords) = CompiledAtlasLoader.TranslateCoordinates(
                    baseTile.SourceId, baseTile.AtlasCoords);
            }
        }
        // For uniform terrain (top == base, bitmask 15), we need the solid fill
        else if (topTileId == baseTileId && bitmask == 15)
        {
            // For compositable tiles, find the solid fill from any transition
            var solidFillCoords = _transitionResolver.ResolveSolidFill(topTileId);
            if (solidFillCoords.HasValue)
            {
                atlasCoords = solidFillCoords.Value;
                sourceId = _transitionResolver.CompiledAtlasSourceId;
                transitionsResolved++;
            }
            else if (baseTile.SourceId == _transitionResolver.CompiledAtlasSourceId)
            {
                // Tile already uses compiled atlas source - use its coordinates directly
                atlasCoords = baseTile.HasAutoTileVariants
                    ? baseTile.GetAutoTileCoords(15)
                    : baseTile.AtlasCoords;
                sourceId = baseTile.SourceId;
            }
            else
            {
                // Fallback: use safe default position (atlas 0,0)
                atlasCoords = Vector2I.Zero;
                sourceId = _transitionResolver.CompiledAtlasSourceId;
            }
        }
        else
        {
            // Standard case: look up transition in compiled map
            var effectiveTopTile = topTile ?? baseTile;
            var fallbackCoords = effectiveTopTile.HasAutoTileVariants
                ? effectiveTopTile.GetAutoTileCoords(bitmask)
                : effectiveTopTile.AtlasCoords;

            var result = _transitionResolver.ResolveWithFallback(
                topTileId, baseTileId, bitmask, effectiveTopTile.SourceId, fallbackCoords);

            atlasCoords = result.AtlasCoords;
            sourceId = result.SourceId;

            if (sourceId == _transitionResolver.CompiledAtlasSourceId)
                transitionsResolved++;
        }

        return (sourceId, atlasCoords);
    }

    /// <summary>
    /// Render non-terrain tiles (structures, decorations, effects) from the data grid.
    /// </summary>
    public void RenderNonTerrainTiles(SimpleMapData mapData)
    {
        // Ensure non-terrain layers have no offset (data grid positions, not visual grid)
        if (_decorationLayer != null) _decorationLayer.Position = Vector2.Zero;
        if (_structureLayer != null) _structureLayer.Position = Vector2.Zero;
        if (_effectLayer != null) _effectLayer.Position = Vector2.Zero;

        var structureCount = 0;
        var decorationCount = 0;
        var effectCount = 0;

        for (var y = 0; y < mapData.Size.Y; y++)
        for (var x = 0; x < mapData.Size.X; x++)
        {
            var position = new Vector2I(x, y);
            var tileId = mapData.GetTileId(position);
            var (sourceId, atlasCoords, layer, tileSize) = GetTileRenderInfoFull(tileId);

            // Skip terrain tiles - they're handled by dual-grid system
            if (layer == TileLayer.Terrain)
                continue;

            var targetLayer = layer switch
            {
                TileLayer.Decoration => _decorationLayer,
                TileLayer.Structure => _structureLayer,
                TileLayer.Effects => _effectLayer,
                _ => null
            };

            if (targetLayer == null)
                continue;

            // Render each cell of multi-tile with proper atlas offset
            for (var dy = 0; dy < tileSize.Y; dy++)
            for (var dx = 0; dx < tileSize.X; dx++)
            {
                var cellPos = new Vector2I(position.X + dx, position.Y + dy);
                if (cellPos.X >= mapData.Size.X || cellPos.Y >= mapData.Size.Y)
                    continue;

                var cellAtlasCoords = new Vector2I(atlasCoords.X + dx, atlasCoords.Y + dy);
                targetLayer.SetCell(cellPos, sourceId, cellAtlasCoords);
            }

            // Track counts for logging
            switch (layer)
            {
                case TileLayer.Structure: structureCount++; break;
                case TileLayer.Decoration: decorationCount++; break;
                case TileLayer.Effects: effectCount++; break;
            }
        }

        if (structureCount + decorationCount + effectCount > 0)
            ILog.Print(
                $"[NON-TERRAIN] Rendered {structureCount} structures, {decorationCount} decorations, " +
                $"{effectCount} effects");
    }

    /// <summary>
    /// Get full render info for a tile including size for multi-tile support.
    /// </summary>
    public (int sourceId, Vector2I atlasCoords, TileLayer layer, Vector2I size) GetTileRenderInfoFull(string tileId)
    {
        if (_tileRegistry == null)
        {
            // Simple fallback when registry unavailable
            var fallbackCoords = tileId is "wall" or "stone" or "rock"
                ? new Vector2I(2, 0)
                : new Vector2I(4, 0);
            return (4, fallbackCoords, TileLayer.Terrain, Vector2I.One);
        }

        var tile = _tileRegistry.GetTile(tileId);
        if (tile == null)
        {
            return (4, new Vector2I(4, 0), TileLayer.Terrain, Vector2I.One);
        }

        return (tile.SourceId, tile.AtlasCoords, tile.Layer, tile.Size);
    }

    /// <summary>
    /// Get render info (sourceId, atlasCoords) for a tile.
    /// </summary>
    public (int sourceId, Vector2I atlasCoords) GetTileRenderInfo(string tileId)
    {
        var (sourceId, atlasCoords, _, _) = GetTileRenderInfoFull(tileId);
        return (sourceId, atlasCoords);
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
