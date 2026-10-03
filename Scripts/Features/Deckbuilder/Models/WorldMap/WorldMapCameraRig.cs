using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Positions and zooms the 2D camera inside the world map viewport, for the biome preview and for a rendered map.
/// </summary>
internal sealed class WorldMapCameraRig
{
    private readonly SubViewport? _viewport;
    private Camera2D? _camera2D;

    internal WorldMapCameraRig(SubViewport? viewport)
    {
        _viewport = viewport;
    }

    internal void ConfigureForPreview()
    {
        if (_viewport == null) return;

        _camera2D ??= _viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D == null) return;

        var viewportCenter = new Vector2(_viewport.Size.X / 2f, _viewport.Size.Y / 2f);
        _camera2D.GlobalPosition = viewportCenter;
        _camera2D.Zoom = Vector2.One;
        _camera2D.Enabled = true;
    }

    internal void ResizeViewport(Vector2I mapSize, int tileSize)
    {
        if (_viewport == null) return;

        _viewport.Size = new Vector2I((mapSize.X + 1) * tileSize, (mapSize.Y + 1) * tileSize);
        _viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;

        _camera2D = _viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D != null) _camera2D.Enabled = true;
    }

    internal void FitToMap(TileMapLayer? terrainLayer, int tileSize)
    {
        if (_camera2D == null || terrainLayer == null || _viewport == null) return;

        var usedRect = terrainLayer.GetUsedRect();
        if (usedRect.Size == Vector2I.Zero) return;

        var mapCenter = new Vector2(
            (usedRect.Position.X * tileSize) + (usedRect.Size.X * tileSize / 2f),
            (usedRect.Position.Y * tileSize) + (usedRect.Size.Y * tileSize / 2f)
        );
        _camera2D.GlobalPosition = mapCenter;

        var mapPixelSize = new Vector2(usedRect.Size.X * tileSize, usedRect.Size.Y * tileSize);
        var viewportSize = _viewport.Size;

        var zoomX = viewportSize.X / mapPixelSize.X;
        var zoomY = viewportSize.Y / mapPixelSize.Y;
        var zoom = Mathf.Min(zoomX, zoomY);

        _camera2D.Zoom = new Vector2(zoom, zoom);
    }
}
