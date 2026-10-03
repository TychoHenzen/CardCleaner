using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Assigns one tile set to the map layers with the filtering and sorting the world map needs.
/// </summary>
internal static class LayerTileSets
{
    internal static void LoadAndAssign(string tilesetPath, params TileMapLayer?[] layers)
    {
        var tileSet = GD.Load<TileSet>(tilesetPath);
        if (tileSet == null)
        {
            ILog.Error($"Failed to load TileSet from {tilesetPath}");
            return;
        }

        Assign(tileSet, layers);
    }

    internal static void Assign(TileSet tileSet, params TileMapLayer?[] layers)
    {
        foreach (var layer in layers)
        {
            if (layer == null) continue;
            layer.TileSet = tileSet;
            layer.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            layer.YSortEnabled = true;
        }
    }
}
