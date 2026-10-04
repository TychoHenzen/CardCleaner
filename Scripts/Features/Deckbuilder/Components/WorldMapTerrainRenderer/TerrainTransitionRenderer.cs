using System.Collections.Generic;
using CardCleaner.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

internal sealed class TerrainTransitionRenderer
{
    private readonly TileMapLayer? _terrainLayer;
    private readonly ITileRegistry? _tileRegistry;
    private readonly TerrainTransitionResolver _resolver;

    public TerrainTransitionRenderer(
        TileMapLayer? terrainLayer,
        ITileRegistry? tileRegistry,
        bool usingCompiledAtlas)
    {
        _terrainLayer = terrainLayer;
        _tileRegistry = tileRegistry;
        _resolver = new TerrainTransitionResolver(usingCompiledAtlas);
    }

    public void Render(SimpleMapData mapData)
    {
        if (_terrainLayer == null || _tileRegistry == null)
            return;

        if (mapData.DecorationOverlays.Count == 0)
        {
            ILog.Print("[DUAL-GRID] No terrain data available, skipping dual-grid rendering");
            return;
        }

        _terrainLayer.Position = Vector2.Zero;
        var state = new RenderState();

        foreach (var (position, (baseTileId, topTileId, bitmask)) in mapData.DecorationOverlays)
            RenderOverlay(position, baseTileId, topTileId, bitmask, state);

        var atlasStatus = _resolver.UsingCompiledAtlas
            ? "compiled atlas"
            : "FALLBACK (no compiled atlas)";
        ILog.Print(
            $"[DUAL-GRID] Rendered {state.TilesRendered} terrain tiles ({state.TransitionsResolved} transitions) " +
            $"using {atlasStatus}");

        if (state.MissingTileIds.Count > 0)
            ILog.Print($"[DUAL-GRID] WARNING: Missing tiles: {string.Join(", ", state.MissingTileIds)}");
    }

    private void RenderOverlay(
        Vector2I position,
        string baseTileId,
        string topTileId,
        int bitmask,
        RenderState state)
    {
        var baseTile = _tileRegistry!.GetTile(baseTileId);
        var topTile = _tileRegistry.GetTile(topTileId);
        if (baseTile == null)
        {
            state.MissingTileIds.Add(baseTileId);
            return;
        }

        var coordinates = _resolver.Resolve(
            baseTile,
            topTile,
            baseTileId,
            topTileId,
            bitmask,
            ref state.TransitionsResolved);
        var variant = (topTile ?? baseTile).GetVariantDefinition(bitmask);
        var renderPosition = variant.HasValue && variant.Value.HasOffset
            ? position + variant.Value.Offset
            : position;
        _terrainLayer!.SetCell(renderPosition, coordinates.SourceId, coordinates.AtlasCoords);
        state.TilesRendered++;
    }

    private sealed class RenderState
    {
        public int TilesRendered;
        public int TransitionsResolved;
        public HashSet<string> MissingTileIds { get; } = new();
    }
}
