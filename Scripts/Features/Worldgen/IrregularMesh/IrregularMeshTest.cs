namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

using Godot;
using System;

/// <summary>
/// Test node for validating irregular mesh generation and rendering.
/// Add this to a scene to visualize the mesh generation pipeline.
/// </summary>
[Tool]
public partial class IrregularMeshTest : Node2D
{
    private IrregularMesh? _mesh;
    private IrregularTerrainRenderer? _renderer;
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
    private int _rings = 6;

    /// <summary>
    /// Radius of each hexagon in world units.
    /// </summary>
    [Export(PropertyHint.Range, "0.5,5.0")]
    public float HexRadius
    {
        get => _hexRadius;
        set { _hexRadius = value; _needsRebuild = true; }
    }
    private float _hexRadius = 1.0f;

    /// <summary>
    /// Probability of merging triangles into quads.
    /// </summary>
    [Export(PropertyHint.Range, "0.0,1.0")]
    public float MergeProbability
    {
        get => _mergeProbability;
        set { _mergeProbability = value; _needsRebuild = true; }
    }
    private float _mergeProbability = 0.7f;

    /// <summary>
    /// Number of Lloyd relaxation iterations.
    /// </summary>
    [Export(PropertyHint.Range, "0,30")]
    public int RelaxationIterations
    {
        get => _relaxationIterations;
        set { _relaxationIterations = value; _needsRebuild = true; }
    }
    private int _relaxationIterations = 15;

    /// <summary>
    /// Random seed for reproducible generation.
    /// </summary>
    [Export]
    public int Seed
    {
        get => _seed;
        set { _seed = value; _needsRebuild = true; }
    }
    private int _seed = 12345;

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
    private int _numIslands = 5;

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
    private float _visualScale = 50f;

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
        _renderer = new IrregularTerrainRenderer();
        AddChild(_renderer);

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

        GD.Print($"[IrregularMeshTest] Generating mesh: rings={_rings}, hexRadius={_hexRadius}, " +
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

        // Reset all terrain
        foreach (var vertex in _mesh.Vertices)
        {
            vertex.TerrainType = 0;
        }

        // Create random islands
        var random = new Random(_seed + 1000);
        var bounds = _mesh.Bounds;

        for (int i = 0; i < _numIslands; i++)
        {
            var center = new Vector2(
                (float)(bounds.Min.X + random.NextDouble() * (bounds.Max.X - bounds.Min.X)),
                (float)(bounds.Min.Y + random.NextDouble() * (bounds.Max.Y - bounds.Min.Y))
            );
            var radius = (float)(1.0 + random.NextDouble() * 2.5);

            foreach (var vertex in _mesh.Vertices)
            {
                if (vertex.Position.DistanceTo(center) < radius)
                {
                    vertex.TerrainType = 1;
                }
            }
        }

        int filledCount = 0;
        foreach (var vertex in _mesh.Vertices)
        {
            if (vertex.TerrainType == 1) filledCount++;
        }
        GD.Print($"[IrregularMeshTest] Terrain assigned: {filledCount}/{_mesh.Vertices.Count} vertices filled");
    }

    private void RenderMesh()
    {
        if (_mesh == null || _renderer == null) return;

        _renderer.TransitionKey = TransitionKey;
        _renderer.VariantSeed = _seed;

        if (UseSolidColors)
        {
            _renderer.RenderTerrainSolid(_mesh);
        }
        else
        {
            _renderer.RenderTerrain(_mesh, TransitionKey);
        }
    }

    public override void _Draw()
    {
        if (_mesh == null) return;

        var transform = Transform2D.Identity.Scaled(new Vector2(_visualScale, -_visualScale));

        // Draw wireframe
        if (_showWireframe)
        {
            foreach (var quad in _mesh.Quads)
            {
                var corners = quad.GetCornerPositions();
                if (corners.Length != 4) continue;

                for (int i = 0; i < 4; i++)
                {
                    var p1 = transform * corners[i];
                    var p2 = transform * corners[(i + 1) % 4];
                    DrawLine(p1, p2, new Color(0.2f, 0.2f, 0.2f, 0.5f), 1f);
                }
            }
        }

        // Draw vertices
        if (_showVertices)
        {
            foreach (var vertex in _mesh.Vertices)
            {
                var pos = transform * vertex.Position;
                var color = vertex.TerrainType == 1
                    ? new Color(0.0f, 0.5f, 0.0f)
                    : new Color(0.6f, 0.5f, 0.4f);
                var size = vertex.IsBoundary ? 4f : 3f;
                DrawCircle(pos, size, color);

                if (vertex.IsBoundary)
                {
                    DrawCircle(pos, size + 1, Colors.White with { A = 0.5f });
                }
            }
        }
    }

    /// <summary>
    /// Default values for property revert (inspector reset button).
    /// </summary>
    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Rings) => true,
            nameof(HexRadius) => true,
            nameof(MergeProbability) => true,
            nameof(RelaxationIterations) => true,
            nameof(Seed) => true,
            nameof(NumIslands) => true,
            nameof(VisualScale) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Rings) => 6,
            nameof(HexRadius) => 1.0f,
            nameof(MergeProbability) => 0.7f,
            nameof(RelaxationIterations) => 15,
            nameof(Seed) => 12345,
            nameof(NumIslands) => 5,
            nameof(VisualScale) => 50f,
            _ => base._PropertyGetRevert(property)
        };
    }
}
