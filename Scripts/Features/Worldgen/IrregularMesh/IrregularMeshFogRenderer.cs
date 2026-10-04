using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Renders fog of war overlay for irregular mesh terrain.
/// Uses an ArrayMesh to draw semi-transparent quads over hidden/revealed cells.
/// </summary>
public partial class IrregularMeshFogRenderer : Node2D
{
    private IrregularMesh? _mesh;
    private IrregularMeshFogOfWar? _fogOfWar;
    private MeshInstance2D? _fogMeshInstance;
    private bool _needsRebuild = true;

    /// <summary>
    /// Color for completely hidden cells (90% opacity to preview map).
    /// </summary>
    [Export]
    public Color HiddenColor { get; set; } = new Color(0, 0, 0, 0.9f);

    /// <summary>
    /// Color for revealed but not currently visible cells.
    /// </summary>
    [Export]
    public Color RevealedColor { get; set; } = new Color(0, 0, 0, 0.5f);

    /// <summary>
    /// Color for currently visible cells (usually transparent).
    /// </summary>
    [Export]
    public Color VisibleColor { get; set; } = new Color(0, 0, 0, 0.0f);

    /// <summary>
    /// Z-index for the fog overlay (should be above terrain and decorations).
    /// </summary>
    [Export]
    public int FogZIndex { get; set; } = 50;

    /// <summary>
    /// Whether to use smooth transitions at fog edges.
    /// </summary>
    [Export]
    public bool SmoothEdges { get; set; } = true;

    /// <summary>
    /// World scale for the mesh.
    /// </summary>
    public float WorldScale { get; set; } = 1f;

    /// <summary>
    /// World offset for the mesh.
    /// </summary>
    public Vector2 WorldOffset { get; set; } = Vector2.Zero;

    public override void _Ready()
    {
        _fogMeshInstance = new MeshInstance2D
        {
            Name = "FogMesh",
            ZIndex = FogZIndex
        };
        AddChild(_fogMeshInstance);
    }

    /// <summary>
    /// Initialize the fog renderer.
    /// </summary>
    /// <param name="mesh">The irregular mesh.</param>
    /// <param name="fogOfWar">The fog of war state tracker.</param>
    public void Initialize(IrregularMesh mesh, IrregularMeshFogOfWar fogOfWar)
    {
        _mesh = mesh;
        _fogOfWar = fogOfWar;

        // Subscribe to fog changes
        _fogOfWar.VisibilityChanged += OnVisibilityChanged;

        _needsRebuild = true;
    }

    /// <summary>
    /// Set world transform for the fog mesh.
    /// </summary>
    public void SetWorldTransform(float scale, Vector2 offset)
    {
        WorldScale = scale;
        WorldOffset = offset;
        _needsRebuild = true;
    }

    /// <summary>
    /// Force a rebuild of the fog mesh.
    /// </summary>
    public void Rebuild()
    {
        _needsRebuild = true;
    }

    public override void _Process(double delta)
    {
        if (_needsRebuild && _mesh != null && _fogOfWar != null)
        {
            RebuildFogMesh();
            _needsRebuild = false;
        }
    }

    private void OnVisibilityChanged(IReadOnlySet<int> changedCells)
    {
        _needsRebuild = true;
    }

    private void RebuildFogMesh()
    {
        if (_mesh == null || _fogOfWar == null || _fogMeshInstance == null)
            return;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var quad in _mesh.Quads)
        {
            var fogState = _fogOfWar.GetFogState(quad.Id);

            // Skip fully visible cells (no fog to render)
            if (fogState == FogState.Visible && !SmoothEdges)
                continue;

            var color = GetFogColor(fogState);

            // Skip fully transparent fog
            if (color.A < 0.01f && !SmoothEdges)
                continue;

            AddQuadToMesh(surfaceTool, quad, color);
        }

        var arrayMesh = surfaceTool.Commit();
        _fogMeshInstance.Mesh = arrayMesh;
    }

    private void AddQuadToMesh(SurfaceTool surfaceTool, MeshQuad quad, Color baseColor)
    {
        var corners = quad.GetCornerPositions();
        if (corners.Length != 4)
            return;

        // Transform corners to world space
        var worldCorners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            worldCorners[i] = corners[i] * WorldScale + WorldOffset;
        }

        Color[] vertexColors;

        if (SmoothEdges && _fogOfWar != null)
        {
            // Calculate per-vertex colors based on adjacent quad states
            vertexColors = GetSmoothVertexColors(quad, baseColor);
        }
        else
        {
            // Use uniform color for the whole quad
            vertexColors = new[] { baseColor, baseColor, baseColor, baseColor };
        }

        // Add two triangles for the quad (CCW winding)
        // Triangle 1: corners[0], corners[1], corners[2]
        surfaceTool.SetColor(vertexColors[0]);
        surfaceTool.AddVertex(new Vector3(worldCorners[0].X, worldCorners[0].Y, 0));

        surfaceTool.SetColor(vertexColors[1]);
        surfaceTool.AddVertex(new Vector3(worldCorners[1].X, worldCorners[1].Y, 0));

        surfaceTool.SetColor(vertexColors[2]);
        surfaceTool.AddVertex(new Vector3(worldCorners[2].X, worldCorners[2].Y, 0));

        // Triangle 2: corners[0], corners[2], corners[3]
        surfaceTool.SetColor(vertexColors[0]);
        surfaceTool.AddVertex(new Vector3(worldCorners[0].X, worldCorners[0].Y, 0));

        surfaceTool.SetColor(vertexColors[2]);
        surfaceTool.AddVertex(new Vector3(worldCorners[2].X, worldCorners[2].Y, 0));

        surfaceTool.SetColor(vertexColors[3]);
        surfaceTool.AddVertex(new Vector3(worldCorners[3].X, worldCorners[3].Y, 0));
    }

    private Color[] GetSmoothVertexColors(MeshQuad quad, Color baseColor)
    {
        var colors = new Color[4];
        var sortedCorners = quad.GetSortedCorners();

        for (int i = 0; i < 4; i++)
        {
            var vertex = sortedCorners[i];

            // Average fog state from all adjacent quads
            float totalAlpha = 0f;
            int count = 0;

            foreach (var adjQuadId in vertex.AdjacentQuadIds)
            {
                var adjState = _fogOfWar!.GetFogState(adjQuadId);
                var adjColor = GetFogColor(adjState);
                totalAlpha += adjColor.A;
                count++;
            }

            float avgAlpha = count > 0 ? totalAlpha / count : baseColor.A;
            colors[i] = new Color(baseColor.R, baseColor.G, baseColor.B, avgAlpha);
        }

        return colors;
    }

    private Color GetFogColor(FogState state)
    {
        return state switch
        {
            FogState.Hidden => HiddenColor,
            FogState.Revealed => RevealedColor,
            FogState.Visible => VisibleColor,
            _ => HiddenColor
        };
    }

    /// <summary>
    /// Get the alpha value for a fog state.
    /// </summary>
    public float GetFogAlpha(FogState state)
    {
        return GetFogColor(state).A;
    }

    /// <summary>
    /// Set custom fog colors.
    /// </summary>
    public void SetFogColors(Color hidden, Color revealed, Color visible)
    {
        HiddenColor = hidden;
        RevealedColor = revealed;
        VisibleColor = visible;
        _needsRebuild = true;
    }

    /// <summary>
    /// Enable or disable smooth edge transitions.
    /// </summary>
    public void SetSmoothEdges(bool enabled)
    {
        if (SmoothEdges != enabled)
        {
            SmoothEdges = enabled;
            _needsRebuild = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _fogOfWar != null)
        {
            _fogOfWar.VisibilityChanged -= OnVisibilityChanged;
        }
        base.Dispose(disposing);
    }
}
