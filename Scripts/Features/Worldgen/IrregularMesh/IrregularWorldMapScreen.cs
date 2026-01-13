using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug;
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

    [Export] public int MeshRings { get; set; } = 5;
    [Export] public float WorldScale { get; set; } = 16f;
    [Export] public float VisionRange { get; set; } = 3f;
    [Export] public float MovementSpeed { get; set; } = 0.15f;
    [Export] public bool ShowDebug { get; set; }
    [Export] public bool FogOfWarEnabled { get; set; } = true;
    [Export] public bool UseRaycastVisibility { get; set; } = false;
    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    [Export] public Label? StatusLabel { get; set; }

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
    private RandomNumberGenerator _rng = new();

    // Extracted components
    private IrregularMapCombatHandler? _combatHandler;
    private IrregularMapEnemyManager? _enemyManager;

    // Tile registry for terrain generation
    private ITileRegistry? _tileRegistry;

    #endregion

    #region Public Properties

    public IrregularMesh? Mesh => _mesh;
    public IrregularMeshMapData? MapData => _mapData;
    public IrregularMeshFogOfWar? FogOfWar => _fogOfWar;
    public IrregularMeshExplorationController? ExplorationController => _explorationController;
    public bool IsInitialized => _isInitialized;

    #endregion

    #region Events

    public event Action? MapGenerated;
    public event Action<int, Vector2>? PlayerMoved;
    public new event Action<IReadOnlySet<int>>? VisibilityChanged;
    public event Action? ExplorationFinished;
    public event Action<int>? EnemySpotted;
    public event Action<int>? EnemyEncountered;
    public event Action<int>? EnemyDefeated;

    #endregion

    #region Lifecycle

    public override void _Ready()
    {
        ServiceLocator.Get<ITileRegistry>(registry => { _tileRegistry = registry; });

        CallDeferred(nameof(SetupScreenMesh));
        CallDeferred(nameof(SetupScreenMaterial));

        CreateChildNodes();

        if (_pendingSeed.HasValue)
        {
            GenerateMap(_pendingSeed.Value);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_fogOfWar != null)
                _fogOfWar.VisibilityChanged -= OnVisibilityChanged;

            if (_explorationController != null)
            {
                _explorationController.PlayerMoved -= OnPlayerMoved;
                _explorationController.PositionUpdated -= OnPositionUpdated;
                _explorationController.ExplorationFinished -= OnExplorationFinished;
            }
        }

        base.Dispose(disposing);
    }

    #endregion

    #region Public API

    /// <summary>
    /// Generate a new map with the given seed.
    /// </summary>
    public void GenerateMap(int seed, CardSignature[]? inputCards = null, CardSignature[]? abilityCards = null)
    {
        if (!IsInsideTree())
        {
            _pendingSeed = seed;
            return;
        }

        _rng.Seed = (ulong)seed;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        GD.Print($"[IrregularWorldMapScreen] Generating map with seed {seed}, rings={MeshRings}");

        // Generate mesh with WFC terrain
        var terrainGen = CreateTerrainGenerator();
        _mesh = inputCards != null && inputCards.Length > 0
            ? terrainGen.GenerateWithCards(MeshRings, inputCards, seed, relaxationIterations: 8)
            : terrainGen.Generate(MeshRings, null, seed, relaxationIterations: 8);

        GD.Print($"[IrregularWorldMapScreen] WFC terrain generation complete ({sw.ElapsedMilliseconds}ms)");

        // Create map data adapter
        _mapData = new IrregularMeshMapData(_mesh) { WorldScale = WorldScale };

        // Initialize components
        InitializeComponents(abilityCards?.ToList());

        // Place enemies and create sprites
        var playerStartCell = FindPlayerStartCell();
        _enemyManager?.PlaceEnemies(seed, playerStartCell);
        _enemyManager?.CreateEnemySprites();

        // Create pathfinder and visibility systems
        _pathfinder = new Pathfinder(_mapData);
        _visibilityChecker = new SimpleVisibilityChecker();

        // Initialize fog of war
        InitializeFogOfWar();

        // Setup rendering
        SetupRendering();
        GD.Print($"[IrregularWorldMapScreen] Rendering setup complete ({sw.ElapsedMilliseconds}ms total)");

        // Upgrade to raycast visibility if enabled
        if (UseRaycastVisibility && Viewport != null)
        {
            _visibilityChecker = new RaycastVisibilityChecker(Viewport.World2D.DirectSpaceState);
            _fogOfWar?.SetVisibilityChecker(_visibilityChecker);
        }

        // Setup exploration
        SetupExploration(playerStartCell);

        _isInitialized = true;
        MapGenerated?.Invoke();

        sw.Stop();
        GD.Print($"[IrregularWorldMapScreen] Map generated in {sw.ElapsedMilliseconds}ms: {_mesh.GetStatistics()}");

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

        _mapData = new IrregularMeshMapData(_mesh) { WorldScale = WorldScale };

        InitializeComponents(null);

        var playerStartCell = FindPlayerStartCell();
        _enemyManager?.PlaceEnemies(seed, playerStartCell);
        _enemyManager?.CreateEnemySprites();

        _pathfinder = new Pathfinder(_mapData);
        _visibilityChecker = new SimpleVisibilityChecker();

        InitializeFogOfWar();
        SetupRendering();

        if (UseRaycastVisibility && Viewport != null)
        {
            _visibilityChecker = new RaycastVisibilityChecker(Viewport.World2D.DirectSpaceState);
            _fogOfWar?.SetVisibilityChecker(_visibilityChecker);
        }

        SetupExploration(playerStartCell);

        _isInitialized = true;
        MapGenerated?.Invoke();

        StartExploration();
    }

    public void StartExploration() => _explorationController?.StartExploration();
    public void StopExploration() => _explorationController?.StopExploration();
    public void ToggleDebug()
    {
        ShowDebug = !ShowDebug;
        if (_debugRenderer != null) _debugRenderer.Visible = ShowDebug;
    }
    public void RevealAll() => _fogOfWar?.RevealAll();

    public void Reset()
    {
        _fogOfWar?.Reset();
        _isInitialized = false;
        _mesh = null;
        _mapData = null;

        _terrainRenderer?.QueueFree();
        _terrainRenderer = null;
        _fogRenderer?.QueueFree();
        _fogRenderer = null;

        _enemyManager?.ClearSprites();

        if (_terrainCollisionBody != null)
        {
            TerrainCollisionShapeGenerator.ClearShapes(_terrainCollisionBody);
            _terrainCollisionBody.QueueFree();
            _terrainCollisionBody = null;
        }
    }

    #endregion

    #region Initialization

    private void InitializeComponents(List<CardSignature>? abilityCards)
    {
        if (_mapData == null || Viewport == null) return;

        _enemyManager = new IrregularMapEnemyManager(_mapData, Viewport);
        _combatHandler = new IrregularMapCombatHandler(_mapData, _rng, abilityCards);
        _combatHandler.CombatEnded += OnCombatEnded;
    }

    private int FindPlayerStartCell()
    {
        if (_mapData == null) return 0;

        for (int i = 0; i < _mapData.CellCount; i++)
        {
            if (_mapData.IsPassable(i))
                return i;
        }
        return 0;
    }

    private void InitializeFogOfWar()
    {
        if (!FogOfWarEnabled || _mapData == null || _visibilityChecker == null) return;

        _fogOfWar = new IrregularMeshFogOfWar(_mapData, _visibilityChecker)
        {
            VisionRange = VisionRange,
            CellsPerUnit = WorldScale
        };
        _fogOfWar.VisibilityChanged += OnVisibilityChanged;
    }

    private MeshTerrainGenerator CreateTerrainGenerator()
    {
        var transitionResolver = new CompiledTransitionResolver();
        var wfcRules = new WfcAdjacencyRules(transitionResolver);
        var allTerrainIds = wfcRules.AllTileIds.ToList();

        var adjacencyRules = new Dictionary<string, HashSet<string>>();
        foreach (var tileId in allTerrainIds)
        {
            var neighbors = wfcRules.GetValidNeighbors(tileId);
            adjacencyRules[tileId] = new HashSet<string>(neighbors);
        }

        var tileToTerrainType = new Dictionary<string, int>();
        var nextTerrainType = 1;

        foreach (var terrainId in allTerrainIds)
        {
            var tileDef = _tileRegistry?.GetTile(terrainId);
            bool isPassable = tileDef?.IsPassable ?? InferPassability(terrainId);

            tileToTerrainType[terrainId] = isPassable ? nextTerrainType++ : 0;
        }

        var generator = new MeshTerrainGenerator(wfcRules, tileToTerrainType, new TileRegistry());

        // Create and register biome registry for card-based generation
        var biomeRegistry = new BiomeRegistry();
        biomeRegistry.RegisterDefaultBiomes();
        generator.SetBiomeRegistry(biomeRegistry);

        return generator;
    }

    private static bool InferPassability(string terrainId)
    {
        var lower = terrainId.ToLowerInvariant();
        return !lower.Contains("rock") &&
               !lower.Contains("wall") &&
               !lower.Contains("water") &&
               !lower.Contains("hedge") &&
               !lower.Contains("lava");
    }

    private void CreateChildNodes()
    {
        if (Viewport == null)
        {
            GD.PrintErr("[IrregularWorldMapScreen] Viewport not assigned!");
            return;
        }

        _camera2D = Viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D == null)
        {
            _camera2D = new Camera2D { Name = "Camera2D", Enabled = true };
            Viewport.AddChild(_camera2D);
        }

        _debugRenderer = new IrregularMeshDebugRenderer
        {
            Name = "DebugRenderer",
            Visible = ShowDebug,
            ZIndex = 90
        };
        Viewport.AddChild(_debugRenderer);

        _playerSprite = new Sprite2D { Name = "PlayerSprite", ZIndex = 100 };
        Viewport.AddChild(_playerSprite);
        _playerSprite.Texture = CreateColoredTexture(new Color(0, 0.8f, 0), 12, 12);
    }

    #endregion

    #region Rendering

    private void SetupRendering()
    {
        if (_mesh == null || Viewport == null) return;

        _terrainRenderer = new IrregularTerrainRenderer { Name = "TerrainRenderer" };
        Viewport.AddChild(_terrainRenderer);
        Viewport.MoveChild(_terrainRenderer, 0);

        _terrainRenderer.Scale = new Vector2(WorldScale, WorldScale);
        _terrainRenderer.SetTileRegistry(_tileRegistry);
        _terrainRenderer.RenderTerrain(_mesh);

        if (FogOfWarEnabled && _fogOfWar != null)
        {
            _fogRenderer = new IrregularMeshFogRenderer { Name = "FogRenderer" };
            _fogRenderer.SetWorldTransform(WorldScale, Vector2.Zero);
            Viewport.AddChild(_fogRenderer);
            _fogRenderer.Initialize(_mesh, _fogOfWar);
        }

        if (UseRaycastVisibility && _mapData != null)
            SetupCollisionShapes();

        _debugRenderer?.Initialize(_mesh, _mapData, WorldScale);
        ConfigureViewportAndCamera();
    }

    private void ConfigureViewportAndCamera()
    {
        if (Viewport == null || _mesh == null || _camera2D == null) return;

        var bounds = _mesh.Bounds;
        var meshWidth = (bounds.Max.X - bounds.Min.X) * WorldScale;
        var meshHeight = (bounds.Max.Y - bounds.Min.Y) * WorldScale;

        var padding = WorldScale * 2;
        Viewport.Size = new Vector2I((int)(meshWidth + padding * 2), (int)(meshHeight + padding * 2));
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;

        var centerX = (bounds.Min.X + bounds.Max.X) / 2 * WorldScale;
        var centerY = (bounds.Min.Y + bounds.Max.Y) / 2 * WorldScale;
        _camera2D.GlobalPosition = new Vector2(centerX, centerY);
        _camera2D.Zoom = Vector2.One;
        _camera2D.Enabled = true;
    }

    private void SetupCollisionShapes()
    {
        if (_mapData == null || Viewport == null) return;

        _terrainCollisionBody = new StaticBody2D { Name = "TerrainCollisionBody" };
        Viewport.AddChild(_terrainCollisionBody);
        TerrainCollisionShapeGenerator.GenerateForIrregularMesh(_mapData, _terrainCollisionBody);
    }

    private void SetupScreenMesh()
    {
        if (ScreenMesh == null) return;
        ViewportScreenSetup.SetupScreenMesh(ScreenMesh);
    }

    private void SetupScreenMaterial()
    {
        if (Viewport == null || ScreenMesh == null) return;
        ViewportScreenSetup.SetupScreenMaterial(Viewport, ScreenMesh);
    }

    #endregion

    #region Exploration

    private void SetupExploration(int startCell)
    {
        if (_mapData == null || Viewport == null) return;

        _explorationController = new IrregularMeshExplorationController
        {
            Name = "ExplorationController",
            StepDelay = MovementSpeed
        };
        Viewport.AddChild(_explorationController);

        _explorationController.Initialize(_mapData, startCell, _visibilityChecker, _fogOfWar);

        _explorationController.PlayerMoved += OnPlayerMoved;
        _explorationController.PositionUpdated += OnPositionUpdated;
        _explorationController.ExplorationFinished += OnExplorationFinished;
        _explorationController.EnemyEncountered += OnEnemyEncountered;
        _explorationController.EnemySpotted += OnEnemySpotted;

        UpdatePlayerPosition(startCell);
    }

    private void UpdatePlayerPosition(int cellId)
    {
        if (_mapData == null || _playerSprite == null) return;
        _playerSprite.Position = _mapData.GetCellCenter(cellId);
    }

    #endregion

    #region Event Handlers

    private void OnPlayerMoved(int cellId, Vector2 worldPos)
    {
        UpdatePlayerPosition(cellId);
        PlayerMoved?.Invoke(cellId, worldPos);

        if (StatusLabel != null)
            StatusLabel.Text = $"Cell: {cellId} Pos: {worldPos:F1}";
    }

    private void OnPositionUpdated(Vector2 worldPos)
    {
        if (_playerSprite != null)
            _playerSprite.Position = worldPos;
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

    private void OnEnemySpotted(int cellId)
    {
        GD.Print($"[IrregularWorldMapScreen] Enemy spotted at cell {cellId}");
        EnemySpotted?.Invoke(cellId);
    }

    private void OnEnemyEncountered(int cellId)
    {
        GD.Print($"[IrregularWorldMapScreen] Enemy encountered at cell {cellId}!");
        EnemyEncountered?.Invoke(cellId);
        _combatHandler?.StartCombat(cellId, this);
    }

    private void OnCombatEnded(int cellId, bool playerWon)
    {
        if (playerWon)
        {
            _mapData?.RemoveEnemySpawn(cellId);
            _enemyManager?.RemoveEnemyAt(cellId);
            EnemyDefeated?.Invoke(cellId);

            if (_mapData?.EnemySpawnCells.Count > 0)
            {
                GD.Print("[IrregularWorldMapScreen] Resuming exploration...");
                _explorationController?.ResetCombatState();
                _explorationController?.StartExploration();
            }
            else
            {
                GD.Print("[IrregularWorldMapScreen] All enemies defeated! Revealing map...");
                RevealAll();
                ExplorationFinished?.Invoke();
            }
        }
        else
        {
            GD.Print("[IrregularWorldMapScreen] Player defeated!");
        }
    }

    #endregion

    #region Helpers

    private static ImageTexture CreateColoredTexture(Color color, int width, int height)
    {
        var image = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    #endregion
}
