using System.Collections.Generic;
using CardCleaner.Scripts.Core.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// Creates and owns the terrain, fog and collision nodes that render a generated map inside the viewport.
/// </summary>
internal sealed class WorldMapRenderLayer
{
    private readonly WorldMapSettings _settings;
    private readonly IrregularMesh _mesh;
    private readonly IrregularMeshMapData _mapData;

    private readonly List<Node> _ownedNodes = new();

    internal WorldMapRenderLayer(WorldMapSettings settings, IrregularMesh mesh, IrregularMeshMapData mapData)
    {
        _settings = settings;
        _mesh = mesh;
        _mapData = mapData;
    }

    /// <summary>
    /// Adds the renderers to the viewport and sizes the viewport and camera to the mesh.
    /// </summary>
    internal void Build(IrregularMeshFogOfWar? fogOfWar)
    {
        var viewport = _settings.Viewport;
        if (viewport == null) return;

        var scale = _settings.WorldScale;
        var terrainRenderer = new IrregularTerrainRenderer { Name = "TerrainRenderer" };
        _ownedNodes.Add(terrainRenderer);
        viewport.AddChild(terrainRenderer);
        viewport.MoveChild(terrainRenderer, 0);

        terrainRenderer.Scale = new Vector2(scale, scale);
        if (_settings.TileRegistry != null)
            terrainRenderer.SetTileRegistry(_settings.TileRegistry);
        terrainRenderer.RenderTerrain(_mesh);

        if (_settings.FogOfWarEnabled && fogOfWar != null)
            BuildFogRenderer(viewport, fogOfWar);

        if (_settings.UseRaycastVisibility)
            BuildCollisionShapes(viewport);

        _settings.DebugRenderer?.Initialize(_mesh, _mapData, scale);
        ConfigureViewportAndCamera(viewport);
    }

    /// <summary>
    /// Frees the renderers and collision body.
    /// </summary>
    internal void Clear()
    {
        foreach (var node in _ownedNodes)
        {
            if (node is StaticBody2D collisionBody)
                TerrainCollisionShapeGenerator.ClearShapes(collisionBody);
            node.QueueFree();
        }

        _ownedNodes.Clear();
    }

    private void BuildFogRenderer(SubViewport viewport, IrregularMeshFogOfWar fogOfWar)
    {
        var fogRenderer = new IrregularMeshFogRenderer { Name = "FogRenderer" };
        _ownedNodes.Add(fogRenderer);
        fogRenderer.SetWorldTransform(_settings.WorldScale, Vector2.Zero);
        viewport.AddChild(fogRenderer);
        fogRenderer.Initialize(_mesh, fogOfWar);
    }

    private void BuildCollisionShapes(SubViewport viewport)
    {
        var collisionBody = new StaticBody2D { Name = "TerrainCollisionBody" };
        _ownedNodes.Add(collisionBody);
        viewport.AddChild(collisionBody);
        TerrainCollisionShapeGenerator.GenerateForIrregularMesh(_mapData, collisionBody);
    }

    private void ConfigureViewportAndCamera(SubViewport viewport)
    {
        var camera = _settings.Camera;
        if (camera == null) return;

        var scale = _settings.WorldScale;
        var bounds = _mesh.Bounds;
        var meshWidth = (bounds.Max.X - bounds.Min.X) * scale;
        var meshHeight = (bounds.Max.Y - bounds.Min.Y) * scale;

        var padding = scale * 2;
        viewport.Size = new Vector2I((int)(meshWidth + padding * 2), (int)(meshHeight + padding * 2));
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;

        var centerX = (bounds.Min.X + bounds.Max.X) / 2 * scale;
        var centerY = (bounds.Min.Y + bounds.Max.Y) / 2 * scale;
        camera.GlobalPosition = new Vector2(centerX, centerY);
        camera.Zoom = Vector2.One;
        camera.Enabled = true;
    }
}
