using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh;

/// <summary>
/// Integrated world map screen for irregular mesh terrain.
/// Combines mesh generation, terrain rendering, pathfinding, fog of war,
/// and exploration into a complete game-ready system.
/// Renders 2D content via SubViewport onto a 3D mesh for world-space display.
/// </summary>
public partial class IrregularWorldMapScreen : Node3D
{
    #region Exports

    /// <summary>
    /// Number of hex rings for mesh generation.
    /// </summary>
    [Export]
    public int MeshRings { get; set; } = 5;

    /// <summary>
    /// World scale for rendering.
    /// </summary>
    [Export]
    public float WorldScale { get; set; } = 16f;

    /// <summary>
    /// Vision range for fog of war (in cells).
    /// </summary>
    [Export]
    public float VisionRange { get; set; } = 3f;

    /// <summary>
    /// Movement speed for exploration.
    /// </summary>
    [Export]
    public float MovementSpeed { get; set; } = 0.15f;

    /// <summary>
    /// Whether to show debug visualization.
    /// </summary>
    [Export]
    public bool ShowDebug { get; set; }

    /// <summary>
    /// Whether fog of war is enabled.
    /// </summary>
    [Export]
    public bool FogOfWarEnabled { get; set; } = true;

    /// <summary>
    /// Whether to use raycast-based visibility checking.
    /// When enabled, generates collision shapes for opaque terrain.
    /// </summary>
    [Export]
    public bool UseRaycastVisibility { get; set; } = false;

    /// <summary>
    /// SubViewport for rendering 2D content.
    /// </summary>
    [Export]
    public SubViewport? Viewport { get; set; }

    /// <summary>
    /// MeshInstance3D to display the viewport texture in world space.
    /// </summary>
    [Export]
    public MeshInstance3D? ScreenMesh { get; set; }

    /// <summary>
    /// Status label for debug info.
    /// </summary>
    [Export]
    public Label? StatusLabel { get; set; }

    #endregion

    #region Private Fields

    private IrregularMesh? _mesh;
    private IrregularMeshMapData? _mapData;
    private IrregularTerrainRenderer? _terrainRenderer;
    private IrregularMeshFogOfWar? _fogOfWar;
    private IrregularMeshFogRenderer? _fogRenderer;
    private IrregularMeshExplorationController? _explorationController;
    private Pathfinder? _pathfinder;
    private IVisibilityChecker? _visibilityChecker;

    private Sprite2D? _playerSprite;
    private IrregularMeshDebugRenderer? _debugRenderer;
    private Camera2D? _camera2D;
    private StaticBody2D? _terrainCollisionBody;

    private bool _isInitialized;
    private int? _pendingSeed;

    #endregion

    #region Public Properties

    /// <summary>
    /// The underlying irregular mesh.
    /// </summary>
    public IrregularMesh? Mesh => _mesh;

    /// <summary>
    /// Map data for navigation and queries.
    /// </summary>
    public IrregularMeshMapData? MapData => _mapData;

    /// <summary>
    /// Fog of war state tracker.
    /// </summary>
    public IrregularMeshFogOfWar? FogOfWar => _fogOfWar;

    /// <summary>
    /// Exploration controller for player movement.
    /// </summary>
    public IrregularMeshExplorationController? ExplorationController => _explorationController;

    /// <summary>
    /// Whether the map has been initialized.
    /// </summary>
    public bool IsInitialized => _isInitialized;

    #endregion

    #region Events

    /// <summary>
    /// Raised when map generation is complete.
    /// </summary>
    public event Action? MapGenerated;

    /// <summary>
    /// Raised when the player moves to a new cell.
    /// </summary>
    public event Action<int, Vector2>? PlayerMoved;

    /// <summary>
    /// Raised when visibility changes.
    /// </summary>
    public new event Action<IReadOnlySet<int>>? VisibilityChanged;

    /// <summary>
    /// Raised when exploration finishes.
    /// </summary>
    public event Action? ExplorationFinished;

    #endregion

    public override void _Ready()
    {
        // Setup 3D rendering pipeline
        CallDeferred(nameof(SetupScreenMesh));
        CallDeferred(nameof(SetupScreenMaterial));

        // Create child nodes inside viewport
        CreateChildNodes();

        if (_pendingSeed.HasValue)
        {
            GenerateMap(_pendingSeed.Value);
        }
    }

    /// <summary>
    /// Generate a new map with the given seed.
    /// </summary>
    public void GenerateMap(int seed)
    {
        if (!IsInsideTree())
        {
            _pendingSeed = seed;
            return;
        }

        GD.Print($"[IrregularWorldMapScreen] Generating map with seed {seed}, rings={MeshRings}");

        // Generate mesh geometry
        var config = new MeshGenerator.GenerationConfig
        {
            Rings = MeshRings,
            Seed = seed,
            RelaxationIterations = 15
        };
        _mesh = MeshGenerator.Generate(config);

        // Assign terrain types
        AssignDefaultTerrain(seed);

        // Create map data adapter
        _mapData = new IrregularMeshMapData(_mesh);

        // Place enemies on the map
        PlaceEnemies(seed);

        // Create subsystems
        _pathfinder = new Pathfinder(_mapData);

        // Create visibility checker - start with simple, upgrade to raycast after collision shapes
        _visibilityChecker = new SimpleVisibilityChecker();

        // Initialize fog of war
        if (FogOfWarEnabled)
        {
            _fogOfWar = new IrregularMeshFogOfWar(_mapData, _visibilityChecker)
            {
                VisionRange = VisionRange
            };
            _fogOfWar.VisibilityChanged += OnVisibilityChanged;
        }

        // Setup rendering (includes collision shape generation)
        SetupRendering();

        // If using raycast visibility, update the visibility checker now that shapes exist
        if (UseRaycastVisibility && Viewport != null)
        {
            _visibilityChecker = new RaycastVisibilityChecker(Viewport.World2D.DirectSpaceState);
            // Update fog of war with the new visibility checker
            _fogOfWar?.SetVisibilityChecker(_visibilityChecker);
        }

        // Setup exploration
        SetupExploration();

        _isInitialized = true;
        MapGenerated?.Invoke();

        GD.Print($"[IrregularWorldMapScreen] Map generated: {_mesh.GetStatistics()}");

        // Start exploration automatically
        StartExploration();
    }

    /// <summary>
    /// Generate map with WFC terrain assignment.
    /// </summary>
    public void GenerateMapWithWfc(
        int seed,
        Dictionary<string, HashSet<string>> adjacencyRules,
        Dictionary<string, int> tileToTerrainType)
    {
        if (!IsInsideTree())
        {
            _pendingSeed = seed;
            return;
        }

        GD.Print($"[IrregularWorldMapScreen] Generating WFC map with seed {seed}");

        var generator = new MeshTerrainGenerator(adjacencyRules, tileToTerrainType);
        _mesh = generator.Generate(MeshRings, null, seed);

        // Create map data adapter
        _mapData = new IrregularMeshMapData(_mesh);

        // Place enemies on the map
        PlaceEnemies(seed);

        // Create subsystems
        _pathfinder = new Pathfinder(_mapData);

        // Create visibility checker - start with simple, upgrade to raycast after collision shapes
        _visibilityChecker = new SimpleVisibilityChecker();

        // Initialize fog of war
        if (FogOfWarEnabled)
        {
            _fogOfWar = new IrregularMeshFogOfWar(_mapData, _visibilityChecker)
            {
                VisionRange = VisionRange
            };
            _fogOfWar.VisibilityChanged += OnVisibilityChanged;
        }

        // Setup rendering (includes collision shape generation)
        SetupRendering();

        // If using raycast visibility, update the visibility checker now that shapes exist
        if (UseRaycastVisibility && Viewport != null)
        {
            _visibilityChecker = new RaycastVisibilityChecker(Viewport.World2D.DirectSpaceState);
            // Update fog of war with the new visibility checker
            _fogOfWar?.SetVisibilityChecker(_visibilityChecker);
        }

        // Setup exploration
        SetupExploration();

        _isInitialized = true;
        MapGenerated?.Invoke();

        // Start exploration automatically
        StartExploration();
    }

    /// <summary>
    /// Start automatic exploration from the current position.
    /// </summary>
    public void StartExploration()
    {
        _explorationController?.StartExploration();
    }

    /// <summary>
    /// Stop automatic exploration.
    /// </summary>
    public void StopExploration()
    {
        _explorationController?.StopExploration();
    }

    /// <summary>
    /// Reset the map to initial state.
    /// </summary>
    public void Reset()
    {
        _fogOfWar?.Reset();
        _isInitialized = false;
        _mesh = null;
        _mapData = null;

        // Clear renderers
        _terrainRenderer?.QueueFree();
        _terrainRenderer = null;
        _fogRenderer?.QueueFree();
        _fogRenderer = null;

        // Clear collision body
        if (_terrainCollisionBody != null)
        {
            TerrainCollisionShapeGenerator.ClearShapes(_terrainCollisionBody);
            _terrainCollisionBody.QueueFree();
            _terrainCollisionBody = null;
        }
    }

    /// <summary>
    /// Toggle debug visualization.
    /// </summary>
    public void ToggleDebug()
    {
        ShowDebug = !ShowDebug;
        UpdateDebugVisibility();
    }

    /// <summary>
    /// Reveal the entire map (disable fog of war).
    /// </summary>
    public void RevealAll()
    {
        _fogOfWar?.RevealAll();
    }

    private void CreateChildNodes()
    {
        if (Viewport == null)
        {
            GD.PrintErr("[IrregularWorldMapScreen] Viewport not assigned!");
            return;
        }

        // Get or create camera for the viewport
        _camera2D = Viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D == null)
        {
            _camera2D = new Camera2D
            {
                Name = "Camera2D",
                Enabled = true
            };
            Viewport.AddChild(_camera2D);
        }

        // Debug renderer (added to viewport)
        _debugRenderer = new IrregularMeshDebugRenderer
        {
            Name = "DebugRenderer",
            Visible = ShowDebug,
            ZIndex = 90
        };
        Viewport.AddChild(_debugRenderer);

        // Player sprite (added to viewport)
        _playerSprite = new Sprite2D
        {
            Name = "PlayerSprite",
            ZIndex = 100
        };
        Viewport.AddChild(_playerSprite);

        // Create a simple colored rectangle for player
        var playerTexture = CreateColoredTexture(new Color(0, 0.8f, 0), 12, 12);
        _playerSprite.Texture = playerTexture;
    }

    private void SetupRendering()
    {
        if (_mesh == null || Viewport == null) return;

        // Create terrain renderer (added to viewport)
        _terrainRenderer = new IrregularTerrainRenderer
        {
            Name = "TerrainRenderer"
        };
        Viewport.AddChild(_terrainRenderer);
        Viewport.MoveChild(_terrainRenderer, 0); // Render behind everything

        _terrainRenderer.Scale = new Vector2(WorldScale, WorldScale);
        _terrainRenderer.RenderTerrain(_mesh);

        // Create fog renderer if enabled (added to viewport)
        if (FogOfWarEnabled && _fogOfWar != null)
        {
            _fogRenderer = new IrregularMeshFogRenderer
            {
                Name = "FogRenderer"
            };
            _fogRenderer.SetWorldTransform(WorldScale, Vector2.Zero);
            Viewport.AddChild(_fogRenderer);

            _fogRenderer.Initialize(_mesh, _fogOfWar);
        }

        // Generate collision shapes for raycast visibility if enabled
        if (UseRaycastVisibility && _mapData != null)
        {
            SetupCollisionShapes();
        }

        // Initialize debug renderer
        _debugRenderer?.Initialize(_mesh, _mapData, WorldScale);

        // Configure viewport and camera
        ConfigureViewportAndCamera();
    }

    private void ConfigureViewportAndCamera()
    {
        if (Viewport == null || _mesh == null || _camera2D == null) return;

        // Calculate mesh bounds to size viewport appropriately
        var bounds = _mesh.Bounds;
        var meshWidth = (bounds.Max.X - bounds.Min.X) * WorldScale;
        var meshHeight = (bounds.Max.Y - bounds.Min.Y) * WorldScale;

        // Add some padding
        var padding = WorldScale * 2;
        var viewportWidth = (int)(meshWidth + padding * 2);
        var viewportHeight = (int)(meshHeight + padding * 2);

        // Set viewport size
        Viewport.Size = new Vector2I(viewportWidth, viewportHeight);
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;

        // Center camera on mesh
        var centerX = (bounds.Min.X + bounds.Max.X) / 2 * WorldScale;
        var centerY = (bounds.Min.Y + bounds.Max.Y) / 2 * WorldScale;
        _camera2D.GlobalPosition = new Vector2(centerX, centerY);
        _camera2D.Zoom = Vector2.One;
        _camera2D.Enabled = true;

        GD.Print($"[IrregularWorldMapScreen] Viewport configured: {viewportWidth}x{viewportHeight}, center=({centerX}, {centerY})");
    }

    private void SetupExploration()
    {
        if (_mapData == null || Viewport == null) return;

        // Find a starting cell (first passable cell)
        var startCell = 0;
        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (_mapData.IsPassable(i))
            {
                startCell = i;
                break;
            }
        }

        // Create exploration controller (added to viewport for 2D processing)
        _explorationController = new IrregularMeshExplorationController
        {
            Name = "ExplorationController",
            StepDelay = MovementSpeed
        };
        Viewport.AddChild(_explorationController);

        _explorationController.Initialize(_mapData, startCell, _visibilityChecker);

        // Subscribe to events
        _explorationController.PlayerMoved += OnPlayerMoved;
        _explorationController.ExplorationFinished += OnExplorationFinished;

        // Position player sprite
        UpdatePlayerPosition(startCell);

        // Initial fog update
        _fogOfWar?.UpdateVisibility(startCell);
    }

    private void AssignDefaultTerrain(int seed = 0)
    {
        if (_mesh == null) return;

        var rng = new RandomNumberGenerator();
        rng.Seed = seed != 0 ? (ulong)seed : (ulong)DateTime.Now.Ticks;

        // 85% grass, 15% water for better connectivity
        foreach (var vertex in _mesh.Vertices)
        {
            vertex.TerrainType = rng.Randf() < 0.85f ? 1 : 0;
        }

        _mesh.UpdateAllCachedProperties();
    }

    /// <summary>
    /// Places enemies on the map.
    /// </summary>
    private void PlaceEnemies(int seed)
    {
        if (_mapData == null) return;

        var rng = new RandomNumberGenerator();
        rng.Seed = (ulong)seed;

        // Find all passable cells
        var passableCells = new System.Collections.Generic.List<int>();
        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (_mapData.IsPassable(i))
                passableCells.Add(i);
        }

        if (passableCells.Count < 2)
        {
            GD.PrintErr("[IrregularWorldMapScreen] Not enough passable cells for enemies!");
            return;
        }

        // Shuffle passable cells
        for (int i = passableCells.Count - 1; i > 0; i--)
        {
            int j = (int)(rng.Randi() % (uint)(i + 1));
            (passableCells[i], passableCells[j]) = (passableCells[j], passableCells[i]);
        }

        // Place 1-3 enemies (skip first cell - player start)
        int enemyCount = rng.RandiRange(1, Mathf.Min(3, passableCells.Count - 1));
        for (int i = 1; i <= enemyCount && i < passableCells.Count; i++)
        {
            _mapData.AddEnemySpawn(passableCells[i]);
        }

        GD.Print($"[IrregularWorldMapScreen] Placed {enemyCount} enemies on map");
    }

    private void UpdatePlayerPosition(int cellId)
    {
        if (_mapData == null || _playerSprite == null) return;

        var worldPos = _mapData.GetCellCenter(cellId) * WorldScale;
        _playerSprite.Position = worldPos;
    }

    private void UpdateDebugVisibility()
    {
        if (_debugRenderer != null)
        {
            _debugRenderer.Visible = ShowDebug;
        }
    }

    private void OnPlayerMoved(int cellId, Vector2 worldPos)
    {
        UpdatePlayerPosition(cellId);
        PlayerMoved?.Invoke(cellId, worldPos);

        if (StatusLabel != null)
        {
            StatusLabel.Text = $"Cell: {cellId} Pos: {worldPos:F1}";
        }
    }

    private void OnVisibilityChanged(IReadOnlySet<int> changedCells)
    {
        VisibilityChanged?.Invoke(changedCells);
    }

    private void OnExplorationFinished()
    {
        ExplorationFinished?.Invoke();
        GD.Print("[IrregularWorldMapScreen] Exploration finished");
    }

    private void SetupCollisionShapes()
    {
        if (_mapData == null || Viewport == null) return;

        // Create collision body for opaque terrain
        _terrainCollisionBody = new StaticBody2D
        {
            Name = "TerrainCollisionBody"
        };
        Viewport.AddChild(_terrainCollisionBody);

        // Generate collision shapes for opaque quads
        TerrainCollisionShapeGenerator.GenerateForIrregularMesh(
            _mapData,
            _terrainCollisionBody);

        GD.Print("[IrregularWorldMapScreen] Generated collision shapes for raycast visibility");
    }

    private static ImageTexture CreateColoredTexture(Color color, int width, int height)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    private void SetupScreenMesh()
    {
        if (ScreenMesh == null) return;

        // Create a custom mesh with explicit UV coordinates
        var arrayMesh = new ArrayMesh();
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Godot.Mesh.ArrayType.Max);

        // Define the quad vertices (matching the desired screen size)
        var vertices = new Vector3[]
        {
            new(-2.6665f, -1.5f, 0), // Bottom-left
            new(2.6665f, -1.5f, 0), // Bottom-right
            new(2.6665f, 1.5f, 0), // Top-right
            new(-2.6665f, 1.5f, 0) // Top-left
        };

        // Critical: UV coordinates that properly map the texture
        var uvs = new Vector2[]
        {
            new(0, 1), // Bottom-left maps to (0,1) - bottom of texture
            new(1, 1), // Bottom-right maps to (1,1) - bottom-right of texture
            new(1, 0), // Top-right maps to (1,0) - top-right of texture
            new(0, 0) // Top-left maps to (0,0) - top-left of texture
        };

        // Triangle indices for two triangles making a quad
        var indices = new int[]
        {
            0, 1, 2, // First triangle
            0, 2, 3 // Second triangle
        };

        // Normals pointing toward camera
        var normals = new[] { Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward };

        // Assign arrays
        arrays[(int)Godot.Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Godot.Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Godot.Mesh.ArrayType.Normal] = normals;
        arrays[(int)Godot.Mesh.ArrayType.Index] = indices;

        // Create the mesh surface
        arrayMesh.AddSurfaceFromArrays(Godot.Mesh.PrimitiveType.Triangles, arrays);

        // Assign the custom mesh
        ScreenMesh.Mesh = arrayMesh;

        GD.Print("[IrregularWorldMapScreen] Custom screen mesh with proper UVs created");
    }

    private void SetupScreenMaterial()
    {
        if (Viewport == null || ScreenMesh == null)
            return;

        // Create a completely new material to avoid any conflicts
        var material = new StandardMaterial3D();

        // Set up the material properties for proper viewport display
        material.AlbedoTexture = Viewport.GetTexture();
        material.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
        material.DisableReceiveShadows = true;
        material.TextureFilter = BaseMaterial3D.TextureFilterEnum.Nearest;
        material.CullMode = BaseMaterial3D.CullModeEnum.Disabled; // Show both sides

        // Critical: Ensure proper UV mapping
        material.Uv1Scale = Vector3.One;
        material.Uv1Offset = -Vector3.One;

        // Force the material as an override
        ScreenMesh.MaterialOverride = material;

        GD.Print("[IrregularWorldMapScreen] Screen material setup complete");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_fogOfWar != null)
            {
                _fogOfWar.VisibilityChanged -= OnVisibilityChanged;
            }

            if (_explorationController != null)
            {
                _explorationController.PlayerMoved -= OnPlayerMoved;
                _explorationController.ExplorationFinished -= OnExplorationFinished;
            }
        }

        base.Dispose(disposing);
    }
}
