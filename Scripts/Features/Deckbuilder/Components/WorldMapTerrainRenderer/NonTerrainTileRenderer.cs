using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

internal sealed class NonTerrainTileRenderer
{
    private readonly TileMapLayer? _decorationLayer;
    private readonly TileMapLayer? _structureLayer;
    private readonly TileMapLayer? _effectLayer;
    private readonly TileRenderInfoResolver _tileInfoResolver;

    public NonTerrainTileRenderer(
        TileMapLayer? decorationLayer,
        TileMapLayer? structureLayer,
        TileMapLayer? effectLayer,
        TileRenderInfoResolver tileInfoResolver)
    {
        _decorationLayer = decorationLayer;
        _structureLayer = structureLayer;
        _effectLayer = effectLayer;
        _tileInfoResolver = tileInfoResolver;
    }

    public void Render(SimpleMapData mapData)
    {
        SetLayerPositions();
        var counts = new RenderCounts();

        for (var y = 0; y < mapData.Size.Y; y++)
        for (var x = 0; x < mapData.Size.X; x++)
            RenderTile(mapData, new Vector2I(x, y), counts);

        counts.Log();
    }

    private void SetLayerPositions()
    {
        if (_decorationLayer != null) _decorationLayer.Position = Vector2.Zero;
        if (_structureLayer != null) _structureLayer.Position = Vector2.Zero;
        if (_effectLayer != null) _effectLayer.Position = Vector2.Zero;
    }

    private void RenderTile(SimpleMapData mapData, Vector2I position, RenderCounts counts)
    {
        var info = _tileInfoResolver.ResolveFull(mapData.GetTileId(position));
        if (info.Layer == TileLayer.Terrain)
            return;

        var targetLayer = GetTargetLayer(info.Layer);
        if (targetLayer == null)
            return;

        for (var dy = 0; dy < info.Size.Y; dy++)
        for (var dx = 0; dx < info.Size.X; dx++)
        {
            var cellPosition = new Vector2I(position.X + dx, position.Y + dy);
            if (cellPosition.X >= mapData.Size.X || cellPosition.Y >= mapData.Size.Y)
                continue;

            var atlasCoords = new Vector2I(info.AtlasCoords.X + dx, info.AtlasCoords.Y + dy);
            targetLayer.SetCell(cellPosition, info.SourceId, atlasCoords);
        }

        counts.Record(info.Layer);
    }

    private TileMapLayer? GetTargetLayer(TileLayer layer) => layer switch
    {
        TileLayer.Decoration => _decorationLayer,
        TileLayer.Structure => _structureLayer,
        TileLayer.Effects => _effectLayer,
        _ => null
    };

    private sealed class RenderCounts
    {
        private int _structureCount;
        private int _decorationCount;
        private int _effectCount;

        public void Record(TileLayer layer)
        {
            switch (layer)
            {
                case TileLayer.Structure: _structureCount++; break;
                case TileLayer.Decoration: _decorationCount++; break;
                case TileLayer.Effects: _effectCount++; break;
            }
        }

        public void Log()
        {
            if (_structureCount + _decorationCount + _effectCount == 0)
                return;

            ILog.Print(
                $"[NON-TERRAIN] Rendered {_structureCount} structures, " +
                $"{_decorationCount} decorations, {_effectCount} effects");
        }
    }
}
