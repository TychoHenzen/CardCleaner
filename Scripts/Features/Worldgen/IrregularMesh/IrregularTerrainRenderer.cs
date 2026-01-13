using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System;
using System.Collections.Generic;
using System.IO;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Core.Services;

/// <summary>
/// Renders an irregular quad mesh as terrain using ArrayMesh with UV mapping.
/// Each quad samples its 4 corner vertices to compute a bitmask, then uses
/// the compiled atlas to find the appropriate auto-tile variant.
/// </summary>
public partial class IrregularTerrainRenderer : Node2D
{
    private const string DefaultAtlasPath = "res://Data/CompiledAtlas/terrain_atlas.png";
    private const int TileSize = 16;

    private MeshInstance2D? _meshInstance;
    private Texture2D? _atlasTexture;
    private CompiledTransitionResolver? _transitionResolver;
    private Vector2 _atlasSize;
    private ITileRegistry? _tileRegistry;

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
    /// Sets the tile registry for looking up tile properties.
    /// REQUIRED for proper terrain resolution.
    /// </summary>
    public void SetTileRegistry(ITileRegistry tileRegistry)
    {
        _tileRegistry = tileRegistry;
    }

    /// <summary>
    /// Render the terrain mesh using the compiled atlas.
    /// Each quad determines its terrain transition from its 4 corner vertices.
    /// </summary>
    public void RenderTerrain(IrregularMesh mesh)
    {
        if (_tileRegistry == null)
        {
            GD.PrintErr("[IrregularTerrainRenderer] CRITICAL: TileRegistry not set! Call SetTileRegistry() before rendering.");
            return;
        }

        if (!LoadResources())
        {
            GD.PrintErr("[IrregularTerrainRenderer] Failed to load atlas resources");
            return;
        }

        var arrayMesh = BuildTerrainMesh(mesh);
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

        // Draw wireframe overlay - placeholder for future implementation
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

        // Create transition resolver
        if (_transitionResolver == null)
        {
            _transitionResolver = new CompiledTransitionResolver();
            GD.Print("[IrregularTerrainRenderer] Created transition resolver");
        }

        return _atlasTexture != null && _transitionResolver != null;
    }

    private ArrayMesh? BuildTerrainMesh(IrregularMesh mesh)
    {
        if (_transitionResolver == null || _atlasTexture == null || _tileRegistry == null)
            return null;

        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        int quadsRendered = 0;
        int quadsSkipped = 0;
        var terrainStats = new Dictionary<string, int>();

        foreach (var quad in mesh.Quads)
        {
            // Phase 3: Sample corners and compute transition
            // 1. Get background tile from the quad (face)
            var backgroundTileId = quad.BackgroundTileId;
            if (string.IsNullOrEmpty(backgroundTileId))
            {
                quadsSkipped++;
                continue;
            }

            // 2. Get sorted corners and find foreground tile type
            var sortedCorners = quad.GetSortedCorners();
            if (sortedCorners.Length != 4)
            {
                quadsSkipped++;
                continue;
            }

            // 3. Find the unique foreground tile from corners (should be at most 1 due to gap constraint)
            string? foregroundTileId = null;
            foreach (var corner in sortedCorners)
            {
                if (!string.IsNullOrEmpty(corner.ForegroundTileId))
                {
                    foregroundTileId = corner.ForegroundTileId;
                    break; // Gap constraint ensures at most 1 type
                }
            }

            // 4. Compute bitmask: which corners have the foreground tile
            // Corner16 format: index 0=SW (bit 2), 1=SE (bit 1), 2=NE (bit 0), 3=NW (bit 3)
            int bitmask = 0;
            if (!string.IsNullOrEmpty(foregroundTileId))
            {
                // Corners are sorted by angle: [0]=SW, [1]=SE, [2]=NE, [3]=NW
                if (sortedCorners[0].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.SW; // 4
                if (sortedCorners[1].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.SE; // 2
                if (sortedCorners[2].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.NE; // 1
                if (sortedCorners[3].ForegroundTileId == foregroundTileId) bitmask |= MeshQuad.NW; // 8
            }

            // 5. Resolve atlas coordinates
            Vector2I? atlasCoords;
            if (string.IsNullOrEmpty(foregroundTileId) || bitmask == 0)
            {
                // Pure background - no foreground tile, show solid background
                atlasCoords = _transitionResolver.ResolveSolidFill(backgroundTileId);
                if (!atlasCoords.HasValue)
                {
                    atlasCoords = _transitionResolver.ResolveAsOuterTerrain(backgroundTileId, 0);
                }
            }
            else if (bitmask == 15)
            {
                // All corners filled - solid foreground
                atlasCoords = _transitionResolver.ResolveSolidFill(foregroundTileId);
            }
            else
            {
                // Transition case - look up "{foreground}|{background}" composite
                int positionSeed = HashPosition(quad.Centroid);
                atlasCoords = _transitionResolver.ResolveTransitionWithVariant(
                    foregroundTileId, backgroundTileId, bitmask, positionSeed, positionSeed);

                // Fallback: try any variant with this bitmask
                if (!atlasCoords.HasValue)
                {
                    atlasCoords = _transitionResolver.ResolveAnyVariant(foregroundTileId, bitmask);
                }
            }

            if (!atlasCoords.HasValue)
            {
                quadsSkipped++;
                continue;
            }

            // Track terrain usage for debugging
            var statsKey = foregroundTileId ?? backgroundTileId;
            if (!terrainStats.ContainsKey(statsKey))
                terrainStats[statsKey] = 0;
            terrainStats[statsKey]++;

            // Get quad corners in sorted order for rendering
            var corners = quad.GetCornerPositions();

            // Calculate UV rectangle for this tile in the atlas
            var uvRect = GetTileUVRect(atlasCoords.Value);

            // Add two triangles for the quad
            // Triangle 1: SW, SE, NE
            // Triangle 2: SW, NE, NW
            AddTriangle(surfaceTool, corners[0], corners[1], corners[2], uvRect, 0, 1, 2);
            AddTriangle(surfaceTool, corners[0], corners[2], corners[3], uvRect, 0, 2, 3);

            quadsRendered++;
        }

        // Log terrain variety stats
        if (terrainStats.Count > 0)
        {
            var stats = string.Join(", ", terrainStats.Select(kv => $"{kv.Key}={kv.Value}"));
            GD.Print($"[IrregularTerrainRenderer] Terrain variety: {stats}");
        }

        GD.Print($"[IrregularTerrainRenderer] Built mesh: {quadsRendered} quads rendered, {quadsSkipped} skipped");

        if (quadsRendered == 0)
            return null;

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    /// <summary>
    /// Resolves the atlas coordinates for a terrain transition.
    /// Uses the same logic as SimpleWorldMapScreen's rendering.
    /// </summary>
    private Vector2I? ResolveTransition(string innerTerrain, string outerTerrain, int bitmask, MeshQuad quad)
    {
        if (_transitionResolver == null)
            return null;

        // Use quad's variant index for consistent variant selection
        var variantIndex = quad.GetVariantIndex();
        int positionSeed = variantIndex >= 0 ? variantIndex : HashPosition(quad.Centroid);

        Vector2I? atlasCoords = null;

        if (bitmask == 0)
        {
            // Pure outer terrain (no filled corners) - solid fill for outer
            atlasCoords = _transitionResolver.ResolveSolidFill(outerTerrain);
            if (!atlasCoords.HasValue)
            {
                atlasCoords = _transitionResolver.ResolveAsOuterTerrain(outerTerrain, 0);
            }
        }
        else if (bitmask == 15)
        {
            // Pure inner terrain (all corners filled) - solid fill for inner
            atlasCoords = _transitionResolver.ResolveSolidFill(innerTerrain);
        }
        else
        {
            // Transition case - need both inner and outer
            atlasCoords = _transitionResolver.ResolveTransitionWithVariant(
                innerTerrain, outerTerrain, bitmask, positionSeed, positionSeed);
        }

        // Fallback: try finding ANY variant with this bitmask for the inner terrain
        if (!atlasCoords.HasValue)
        {
            atlasCoords = _transitionResolver.ResolveAnyVariant(innerTerrain, bitmask);
        }

        return atlasCoords;
    }

    /// <summary>
    /// Finds an appropriate outer terrain by examining quad corners.
    /// Returns the first different tile found, or null if all corners match.
    /// </summary>
    private string? FindOuterTerrainFromQuad(MeshQuad quad, string innerTileId, ITileRegistry tileRegistry)
    {
        var corners = quad.GetSortedCorners();

        foreach (var corner in corners)
        {
            if (corner.TileId != null && corner.TileId != innerTileId)
            {
                var otherTileDef = tileRegistry.GetTile(corner.TileId);
                if (otherTileDef != null)
                {
                    // Use the other tile's inner terrain as our outer
                    return otherTileDef.InnerTerrainId ?? corner.TileId;
                }
            }
        }

        return null;
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
