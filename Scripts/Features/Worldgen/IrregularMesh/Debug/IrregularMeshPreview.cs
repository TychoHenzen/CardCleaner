namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug;

using Godot;
using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug.Painting;

/// <summary>
/// Editor preview node for visualizing irregular mesh generation and rendering.
/// Add this to a scene to visualize the mesh generation pipeline in the Godot editor.
/// </summary>
[Tool]
public partial class IrregularMeshPreview : Node2D
{
    private IrregularMesh? _mesh;
    private IrregularTerrainRenderer? _renderer;
    private ITileRegistry? _tileRegistry;
    private bool _needsRebuild = true;

    /// <summary>
    /// Number of hexagonal rings (determines mesh size).
    /// </summary>
    [Export(PropertyHint.Range, "1,20")]
    public int Rings
    {
        get => _rings;
        set { _rings = value; _needsRebuild = true; }
    }
    private int _rings = PreviewDefaults.Rings;

    /// <summary>
    /// Radius of each hexagon in world units.
    /// </summary>
    [Export(PropertyHint.Range, "0.5,5.0")]
    public float HexRadius
    {
        get => _hexRadius;
        set { _hexRadius = value; _needsRebuild = true; }
    }
    private float _hexRadius = PreviewDefaults.HexRadius;

    /// <summary>
    /// Probability of merging triangles into quads.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float MergeProbability
    {
        get => _mergeProbability;
        set { _mergeProbability = value; _needsRebuild = true; }
    }
    private float _mergeProbability = PreviewDefaults.MergeProbability;

    /// <summary>
    /// Number of Lloyd relaxation iterations.
    /// </summary>
    [Export(PropertyHint.Range, "0,30")]
    public int RelaxationIterations
    {
        get => _relaxationIterations;
        set { _relaxationIterations = value; _needsRebuild = true; }
    }
    private int _relaxationIterations = PreviewDefaults.RelaxationIterations;

    /// <summary>
    /// Random seed for reproducible generation.
    /// </summary>
    [Export]
    public int Seed
    {
        get => _seed;
        set { _seed = value; _needsRebuild = true; }
    }
    private int _seed = PreviewDefaults.Seed;

    /// <summary>
    /// Transition key for atlas lookup (e.g., "grass3|base_grass1").
    /// </summary>
    [Export]
    public string TransitionKey { get; set; } = "mound1|base_sand2";

    /// <summary>
    /// Use solid colors instead of atlas textures (for testing).
    /// </summary>
    [Export]
    public bool UseSolidColors { get; set; }

    /// <summary>
    /// Number of terrain islands to generate.
    /// </summary>
    [Export(PropertyHint.Range, "1,20")]
    public int NumIslands
    {
        get => _numIslands;
        set { _numIslands = value; _needsRebuild = true; }
    }
    private int _numIslands = PreviewDefaults.NumIslands;

    /// <summary>
    /// Show wireframe overlay.
    /// </summary>
    [Export]
    public bool ShowWireframe
    {
        get => _showWireframe;
        set
        {
            _showWireframe = value;
            QueueRedraw();
        }
    }
    private bool _showWireframe = true;

    /// <summary>
    /// Show vertex markers.
    /// </summary>
    [Export]
    public bool ShowVertices
    {
        get => _showVertices;
        set
        {
            _showVertices = value;
            QueueRedraw();
        }
    }
    private bool _showVertices;

    /// <summary>
    /// Scale for visualization (pixels per world unit).
    /// </summary>
    [Export(PropertyHint.Range, "10,200")]
    public float VisualScale
    {
        get => _visualScale;
        set
        {
            _visualScale = value;
            UpdateTransform();
        }
    }
    private float _visualScale = PreviewDefaults.VisualScale;

    /// <summary>
    /// Button to regenerate mesh.
    /// </summary>
    [Export]
    public bool Regenerate
    {
        get => false;
        set { if (value) GenerateMesh(); }
    }

    public override void _Ready()
    {
        // Initialize tile registry for proper terrain lookups
        try
        {
            _tileRegistry = new TileRegistry();
            GD.Print(
                $"[IrregularMeshPreview] Loaded TileRegistry with " +
                $"{((TileRegistry)_tileRegistry).GetAllTiles().GetEnumerator().MoveNext()} tiles");
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[IrregularMeshPreview] Failed to load TileRegistry: {ex.Message}");
        }

        _renderer = new IrregularTerrainRenderer();
        AddChild(_renderer);

        // Pass tile registry to renderer for proper terrain lookups
        if (_tileRegistry != null)
        {
            _renderer.SetTileRegistry(_tileRegistry);
        }

        UpdateTransform();

        if (!Engine.IsEditorHint())
        {
            GenerateMesh();
        }
    }

    public override void _Process(double delta)
    {
        if (_needsRebuild && !Engine.IsEditorHint())
        {
            GenerateMesh();
        }
    }

    private void UpdateTransform()
    {
        if (_renderer != null)
        {
            _renderer.Scale = new Vector2(_visualScale, -_visualScale); // Flip Y for screen coords
        }
    }

    private void GenerateMesh()
    {
        _needsRebuild = false;

        var config = new MeshGenerator.GenerationConfig
        {
            Rings = _rings,
            HexRadius = _hexRadius,
            MergeProbability = _mergeProbability,
            RelaxationIterations = _relaxationIterations,
            Seed = _seed
        };

        GD.Print($"[IrregularMeshPreview] Generating mesh: rings={_rings}, hexRadius={_hexRadius}, " +
                 $"merge={_mergeProbability:F2}, relax={_relaxationIterations}, seed={_seed}");

        _mesh = MeshGenerator.Generate(config);

        // Assign terrain (random islands)
        AssignTerrainIslands();

        // Render
        RenderMesh();

        QueueRedraw();
    }

    private void AssignTerrainIslands()
    {
        if (_mesh == null) return;

        int filledCount = IslandTerrainAssigner.Assign(_mesh, _seed, _numIslands);
        GD.Print($"[IrregularMeshPreview] Terrain assigned: {filledCount}/{_mesh.Vertices.Count} vertices filled");
    }

    private void RenderMesh()
    {
        if (_mesh == null || _renderer == null) return;

        _renderer.VariantSeed = _seed;

        if (UseSolidColors)
        {
            _renderer.RenderTerrainSolid(_mesh);
        }
        else
        {
            // Use RenderTerrain with transition key override if specified
            _renderer.RenderTerrain(_mesh);
        }
    }

    public override void _Draw()
    {
        if (_mesh == null) return;

        var transform = Transform2D.Identity.Scaled(new Vector2(_visualScale, -_visualScale));
        var painter = new PreviewMeshPainter(this, _mesh, transform);

        if (_showWireframe)
        {
            painter.DrawWireframe();
        }

        if (_showVertices)
        {
            painter.DrawVertices();
        }
    }

    /// <summary>
    /// Default values for property revert (inspector reset button).
    /// </summary>
    public override bool _PropertyCanRevert(StringName property)
    {
        return PreviewDefaults.Find(property.ToString()).HasValue || base._PropertyCanRevert(property);
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return PreviewDefaults.Find(property.ToString()) ?? base._PropertyGetRevert(property);
    }
}
