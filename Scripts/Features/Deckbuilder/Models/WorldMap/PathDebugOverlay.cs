using System.Collections.Generic;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Draws the exploration path and target onto the overlay layer and remembers which cells it painted.
/// </summary>
internal sealed class PathDebugOverlay
{
    private readonly HashSet<Vector2I> _renderedTiles = new();

    internal void ForgetRenderedTiles() => _renderedTiles.Clear();

    internal void Show(
        TileMapLayer overlayLayer,
        WorldMapTerrainRenderer terrainRenderer,
        IReadOnlyList<Vector2I> path,
        Vector2I? target)
    {
        // Clear previous debug overlay tiles
        foreach (var pos in _renderedTiles)
            overlayLayer.EraseCell(pos);
        _renderedTiles.Clear();

        // Render new path tiles
        var (pathSourceId, pathAtlasCoords) = terrainRenderer.GetTileRenderInfo("debug_path");
        foreach (var pos in path)
        {
            overlayLayer.SetCell(pos, pathSourceId, pathAtlasCoords);
            _renderedTiles.Add(pos);
        }

        // Render target tile
        if (target.HasValue)
        {
            var (targetSourceId, targetAtlasCoords) = terrainRenderer.GetTileRenderInfo("debug_target");
            overlayLayer.SetCell(target.Value, targetSourceId, targetAtlasCoords);
            _renderedTiles.Add(target.Value);
        }
    }
}
