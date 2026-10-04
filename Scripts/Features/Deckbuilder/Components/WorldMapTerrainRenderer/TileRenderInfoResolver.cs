using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Enumeration;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components.WorldMapTerrainRendererSupport;

internal sealed class TileRenderInfoResolver
{
    private readonly ITileRegistry? _tileRegistry;

    public TileRenderInfoResolver(ITileRegistry? tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    public TileRenderInfo ResolveFull(string tileId)
    {
        if (_tileRegistry == null)
        {
            var fallbackCoords = tileId is "wall" or "stone" or "rock"
                ? new Godot.Vector2I(2, 0)
                : new Godot.Vector2I(4, 0);
            return new TileRenderInfo(4, fallbackCoords, TileLayer.Terrain, Godot.Vector2I.One);
        }

        var tile = _tileRegistry.GetTile(tileId);
        return tile == null
            ? new TileRenderInfo(4, new Godot.Vector2I(4, 0), TileLayer.Terrain, Godot.Vector2I.One)
            : new TileRenderInfo(tile.SourceId, tile.AtlasCoords, tile.Layer, tile.Size);
    }

    public TileRenderCoordinates Resolve(string tileId)
    {
        var info = ResolveFull(tileId);
        return new TileRenderCoordinates(info.SourceId, info.AtlasCoords);
    }
}
