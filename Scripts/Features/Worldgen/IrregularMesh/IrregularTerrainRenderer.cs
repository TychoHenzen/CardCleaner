namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;

/// <summary>
/// Renders an irregular quad mesh as terrain using ArrayMesh with UV mapping.
/// Each quad is rendered as two triangles with UVs pointing to the appropriate
/// auto-tile in the compiled atlas based on its Corner16 bitmask.
/// </summary>
public partial class IrregularTerrainRenderer : Node2D
{
    private const string DefaultAtlasPath = "res://Data/CompiledAtlas/terrain_atlas.png";
    private const string DefaultTransitionMapPath = "res://Data/CompiledAtlas/transition_map.json";
    private const int TileSize = 16;

    private MeshInstance2D? _meshInstance;
    private Texture2D? _atlasTexture;
    private CompiledTransitionMap? _transitionMap;
    private Vector2 _atlasSize;

    /// <summary>
    /// The transition key to use for rendering (e.g., "grass3|base_grass1").
    /// </summary>
    [Export]
    public string TransitionKey { get; set; } = "grass3|base_grass1";

    /// <summary>
    /// Seed for selecting tile variants (for reproducible randomization).
    /// </summary>
    [Export]
    public int VariantSeed { get; set; } = 12345;

    /// <summary>
    /// Show debug wireframe overlay.
    /// </summary>
    [Export]
    public bool ShowWireframe { get; set; }

    /// <summary>
    /// Color for wireframe overlay.
    /// </summary>
    [Export]
    public Color WireframeColor { get; set; } = new(0.2f, 0.2f, 0.2f, 0.5f);

    public override void _Ready()
    {
        _meshInstance = new MeshInstance2D();
        AddChild(_meshInstance);
    }

    /// <summary>
    /// Render the terrain mesh using the compiled atlas.
    /// </summary>
    public void RenderTerrain(IrregularMesh mesh, string? transitionKey = null)
    {
        transitionKey ??= TransitionKey;

        if (!LoadResources())
        {
            GD.PrintErr("[IrregularTerrainRenderer] Failed to load atlas resources");
            return;
        }

        var arrayMesh = BuildTerrainMesh(mesh, transitionKey);
        if (arrayMesh == null)
        {
            GD.PrintErr("[IrregularTerrainRenderer] Failed to build terrain mesh");
            return;
        }

        _meshInstance!.Mesh = arrayMesh;
        _meshInstance.Texture = _atlasTexture;

        GD.Print($"[IrregularTerrainRenderer] Rendered {mesh.Quads.Count} quads");

        QueueRedraw(); // For wireframe overlay
    }

    public override void _Draw()
    {
        if (!ShowWireframe || _meshInstance?.Mesh == null)
            return;

        // Draw wireframe overlay - we'd need to store the mesh reference
        // For now, this is a placeholder
    }

    /// <summary>
    /// Render terrain with a simple solid color per bitmask (for testing without atlas).
    /// </summary>
    public void RenderTerrainSolid(IrregularMesh mesh)
    {
        var arrayMesh = BuildSolidColorMesh(mesh);
        if (arrayMesh == null)
        {
            GD.PrintErr("[IrregularTerrainRenderer] Failed to build solid color mesh");
            return;
        }

        _meshInstance!.Mesh = arrayMesh;
        _meshInstance.Texture = null;

        GD.Print($"[IrregularTerrainRenderer] Rendered {mesh.Quads.Count} quads (solid color)");
    }

    private bool LoadResources()
    {
        // Load atlas texture
        if (_atlasTexture == null)
        {
            if (ResourceLoader.Exists(DefaultAtlasPath))
            {
                _atlasTexture = ResourceLoader.Load<Texture2D>(DefaultAtlasPath);
            }
            else
            {
                // Fallback: load from disk
                var absolutePath = ProjectSettings.GlobalizePath(DefaultAtlasPath);
                if (File.Exists(absolutePath))
                {
                    var image = Image.LoadFromFile(absolutePath);
                    if (image != null)
                    {
                        _atlasTexture = ImageTexture.CreateFromImage(image);
                    }
                }
            }

            if (_atlasTexture == null)
            {
                GD.PrintErr($"[IrregularTerrainRenderer] Failed to load atlas: {DefaultAtlasPath}");
                return false;
            }

            _atlasSize = _atlasTexture.GetSize();
            GD.Print($"[IrregularTerrainRenderer] Loaded atlas: {_atlasSize.X}x{_atlasSize.Y}");
        }

        // Load transition map
        if (_transitionMap == null)
        {
            var absolutePath = ProjectSettings.GlobalizePath(DefaultTransitionMapPath);
            if (!File.Exists(absolutePath))
            {
                GD.PrintErr($"[IrregularTerrainRenderer] Transition map not found: {absolutePath}");
                return false;
            }

            try
            {
                var json = File.ReadAllText(absolutePath);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };
                _transitionMap = JsonSerializer.Deserialize<CompiledTransitionMap>(json, options);
                GD.Print($"[IrregularTerrainRenderer] Loaded transition map with {_transitionMap?.Transitions.Count ?? 0} transitions");
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[IrregularTerrainRenderer] Failed to load transition map: {ex.Message}");
                return false;
            }
        }

        return _atlasTexture != null && _transitionMap != null;
    }

    private ArrayMesh? BuildTerrainMesh(IrregularMesh mesh, string transitionKey)
    {
        if (_transitionMap == null || _atlasTexture == null)
            return null;

        var (borderId, outerTerrain) = CompiledTransitionMap.ParseKey(transitionKey);

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        int quadsRendered = 0;
        int quadsSkipped = 0;

        foreach (var quad in mesh.Quads)
        {
            var bitmask = quad.ComputeCorner16Bitmask();

            // Get atlas coordinates for this bitmask
            var positionSeed = HashPosition(quad.Centroid);
            var atlasCoords = _transitionMap.GetVariantCoordsWithRandom(borderId, outerTerrain, bitmask, positionSeed);

            if (!atlasCoords.HasValue)
            {
                quadsSkipped++;
                continue;
            }

            // Get quad corners in sorted order: SW, SE, NE, NW
            var corners = quad.GetCornerPositions();
            if (corners.Length != 4)
            {
                quadsSkipped++;
                continue;
            }

            // Calculate UV rectangle for this tile in the atlas
            var uvRect = GetTileUVRect(atlasCoords.Value);

            // Add two triangles for the quad
            // Triangle 1: SW, SE, NE
            // Triangle 2: SW, NE, NW
            AddTriangle(surfaceTool, corners[0], corners[1], corners[2], uvRect, 0, 1, 2);
            AddTriangle(surfaceTool, corners[0], corners[2], corners[3], uvRect, 0, 2, 3);

            quadsRendered++;
        }

        GD.Print($"[IrregularTerrainRenderer] Built mesh: {quadsRendered} quads rendered, {quadsSkipped} skipped");

        if (quadsRendered == 0)
            return null;

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    private ArrayMesh? BuildSolidColorMesh(IrregularMesh mesh)
    {
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        foreach (var quad in mesh.Quads)
        {
            var bitmask = quad.ComputeCorner16Bitmask();
            var color = GetBitmaskColor(bitmask);

            var corners = quad.GetCornerPositions();
            if (corners.Length != 4)
                continue;

            // Triangle 1: SW, SE, NE
            surfaceTool.SetColor(color);
            surfaceTool.AddVertex(new Vector3(corners[0].X, corners[0].Y, 0));
            surfaceTool.SetColor(color);
            surfaceTool.AddVertex(new Vector3(corners[1].X, corners[1].Y, 0));
            surfaceTool.SetColor(color);
            surfaceTool.AddVertex(new Vector3(corners[2].X, corners[2].Y, 0));

            // Triangle 2: SW, NE, NW
            surfaceTool.SetColor(color);
            surfaceTool.AddVertex(new Vector3(corners[0].X, corners[0].Y, 0));
            surfaceTool.SetColor(color);
            surfaceTool.AddVertex(new Vector3(corners[2].X, corners[2].Y, 0));
            surfaceTool.SetColor(color);
            surfaceTool.AddVertex(new Vector3(corners[3].X, corners[3].Y, 0));
        }

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    private Rect2 GetTileUVRect(Vector2I atlasCoords)
    {
        // Convert tile coordinates to UV coordinates (0-1 range)
        float u = (atlasCoords.X * TileSize) / _atlasSize.X;
        float v = (atlasCoords.Y * TileSize) / _atlasSize.Y;
        float uSize = TileSize / _atlasSize.X;
        float vSize = TileSize / _atlasSize.Y;

        return new Rect2(u, v, uSize, vSize);
    }

    private void AddTriangle(SurfaceTool st, Vector2 p0, Vector2 p1, Vector2 p2,
                             Rect2 uvRect, int uvIdx0, int uvIdx1, int uvIdx2)
    {
        // UV corners in order: SW(0), SE(1), NE(2), NW(3)
        // UV rect: position is top-left (SW in our Y-up world)
        var uvCorners = new Vector2[4];
        uvCorners[0] = new Vector2(uvRect.Position.X, uvRect.Position.Y + uvRect.Size.Y); // SW (bottom-left in UV)
        uvCorners[1] = new Vector2(uvRect.Position.X + uvRect.Size.X, uvRect.Position.Y + uvRect.Size.Y); // SE (bottom-right)
        uvCorners[2] = new Vector2(uvRect.Position.X + uvRect.Size.X, uvRect.Position.Y); // NE (top-right)
        uvCorners[3] = new Vector2(uvRect.Position.X, uvRect.Position.Y); // NW (top-left)

        st.SetUV(uvCorners[uvIdx0]);
        st.AddVertex(new Vector3(p0.X, p0.Y, 0));

        st.SetUV(uvCorners[uvIdx1]);
        st.AddVertex(new Vector3(p1.X, p1.Y, 0));

        st.SetUV(uvCorners[uvIdx2]);
        st.AddVertex(new Vector3(p2.X, p2.Y, 0));
    }

    private static int HashPosition(Vector2 pos)
    {
        // Simple position hash for deterministic variant selection
        int x = (int)(pos.X * 1000);
        int y = (int)(pos.Y * 1000);
        return x * 31 + y;
    }

    private static Color GetBitmaskColor(int bitmask)
    {
        // Color based on how many corners are filled
        int filledCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if ((bitmask & (1 << i)) != 0)
                filledCount++;
        }

        return filledCount switch
        {
            0 => new Color(0.91f, 0.86f, 0.77f), // Empty - sand/dirt
            1 => new Color(0.49f, 0.70f, 0.26f), // One corner - light grass
            2 => new Color(0.33f, 0.55f, 0.18f), // Two corners - medium grass
            3 => new Color(0.20f, 0.41f, 0.12f), // Three corners - darker grass
            4 => new Color(0.18f, 0.31f, 0.09f), // Full - dark grass
            _ => Colors.Magenta
        };
    }
}
