using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Tiles.Loading.CompiledAtlas;

/// <summary>
/// Paints cells on the world map's tile layers. Every cell drawn onto a layer that may hold the compiled atlas goes
/// through <see cref="Set" />, which first creates the atlas tile the cell shows when the atlas has deferred it
/// (<see cref="CompiledAtlasTiles" />). On any other tile set it only sets the cell.
/// </summary>
internal static class AtlasCells
{
    internal static void Set(TileMapLayer layer, Vector2I cell, int sourceId, Vector2I atlasCoords)
    {
        var tileSet = layer.TileSet;
        if (tileSet != null)
            CompiledAtlasLoader.TilesFor(tileSet)?.Ensure(sourceId, atlasCoords);
        layer.SetCell(cell, sourceId, atlasCoords);
    }
}
