using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Rendering;

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
        // Use nearest-neighbor filtering to prevent texture bleeding at tile edges
        _meshInstance.TextureFilter = TextureFilterEnum.Nearest;
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
            GD.PrintErr(
                "[IrregularTerrainRenderer] CRITICAL: TileRegistry not set! " +
                "Call SetTileRegistry() before rendering.");
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
        var arrayMesh = SolidColorMeshBuilder.Build(mesh);
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
        if (_atlasTexture == null && !LoadAtlas())
            return false;

        if (_transitionResolver == null)
        {
            _transitionResolver = new CompiledTransitionResolver();
            GD.Print("[IrregularTerrainRenderer] Created transition resolver");
        }

        return true;
    }

    private bool LoadAtlas()
    {
        _atlasTexture = TerrainAtlasLoader.Load(DefaultAtlasPath);
        if (_atlasTexture == null)
        {
            GD.PrintErr($"[IrregularTerrainRenderer] Failed to load atlas: {DefaultAtlasPath}");
            return false;
        }

        _atlasSize = _atlasTexture.GetSize();
        GD.Print($"[IrregularTerrainRenderer] Loaded atlas: {_atlasSize.X}x{_atlasSize.Y}");
        return true;
    }

    private ArrayMesh? BuildTerrainMesh(IrregularMesh mesh)
    {
        if (_transitionResolver == null || _atlasTexture == null || _tileRegistry == null)
            return null;

        var quadResolver = new TerrainQuadResolver(_transitionResolver);
        var meshWriter = new TerrainQuadMeshWriter(_atlasSize, TileSize);
        var surfaceTool = new SurfaceTool();
        surfaceTool.Begin(Mesh.PrimitiveType.Triangles);

        int quadsRendered = 0;
        int quadsSkipped = 0;
        var terrainStats = new Dictionary<string, int>();

        foreach (var quad in mesh.Quads)
        {
            var choice = quadResolver.Resolve(quad);
            if (choice == null)
            {
                quadsSkipped++;
                continue;
            }

            terrainStats[choice.Value.StatsKey] = terrainStats.GetValueOrDefault(choice.Value.StatsKey) + 1;
            meshWriter.AddQuad(surfaceTool, quad.GetCornerPositions(), choice.Value.AtlasCoords);
            quadsRendered++;
        }

        LogBuildSummary(terrainStats, quadsRendered, quadsSkipped);

        if (quadsRendered == 0)
            return null;

        surfaceTool.GenerateNormals();
        return surfaceTool.Commit();
    }

    private static void LogBuildSummary(Dictionary<string, int> terrainStats, int quadsRendered, int quadsSkipped)
    {
        if (terrainStats.Count > 0)
        {
            var stats = string.Join(", ", terrainStats.Select(kv => $"{kv.Key}={kv.Value}"));
            GD.Print($"[IrregularTerrainRenderer] Terrain variety: {stats}");
        }

        GD.Print($"[IrregularTerrainRenderer] Built mesh: {quadsRendered} quads rendered, {quadsSkipped} skipped");
    }
}
