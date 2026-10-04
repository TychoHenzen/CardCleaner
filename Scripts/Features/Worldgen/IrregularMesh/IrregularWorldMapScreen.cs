using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.Debug;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
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
    private WorldMapSession? _session;

    private Sprite2D? _playerSprite;
    private IrregularMeshDebugRenderer? _debugRenderer;
    private Camera2D? _camera2D;

    private bool _isInitialized;
    private Action? _pendingGeneration;
    private RandomNumberGenerator _rng = new();

    // Tile registry for terrain generation
    private ITileRegistry? _tileRegistry;

    #endregion

    #region Public Properties

    public IrregularMesh? Mesh => _mesh;
    public IrregularMeshMapData? MapData => _mapData;
    public IrregularMeshFogOfWar? FogOfWar => _session?.FogOfWar;
    public IrregularMeshExplorationController? ExplorationController => _session?.ExplorationController;
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

        var pendingGeneration = _pendingGeneration;
        _pendingGeneration = null;
        pendingGeneration?.Invoke();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _session?.Detach();

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
            _pendingGeneration = () => GenerateMap(seed, inputCards, abilityCards);
            return;
        }

        _rng.Seed = (ulong)seed;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        GD.Print($"[IrregularWorldMapScreen] Generating map with seed {seed}, rings={MeshRings}");

        // Generate mesh with WFC terrain
        var terrainGen = TerrainGeneratorFactory.Create(_tileRegistry);
        var mesh = inputCards != null && inputCards.Length > 0
            ? terrainGen.GenerateWithCards(MeshRings, inputCards, seed, relaxationIterations: 8)
            : terrainGen.Generate(MeshRings, null, seed, relaxationIterations: 8);

        GD.Print($"[IrregularWorldMapScreen] WFC terrain generation complete ({sw.ElapsedMilliseconds}ms)");

        var session = BeginSession(mesh);
        var playerStartCell = session.Prepare(seed, abilityCards?.ToList());
        GD.Print($"[IrregularWorldMapScreen] Rendering setup complete ({sw.ElapsedMilliseconds}ms total)");

        CompleteSession(session, playerStartCell);

        sw.Stop();
        GD.Print($"[IrregularWorldMapScreen] Map generated in {sw.ElapsedMilliseconds}ms: {mesh.GetStatistics()}");

        StartExploration();
    }

    /// <summary>
    /// Generate map with WFC terrain assignment.
    /// </summary>
    public void GenerateMapWithWfc(
        int seed,
        Dictionary<string, HashSet<string>> adjacencyRules,
        Dictionary<string, int> tileToTerrainType,
        CardSignature[]? abilityCards = null)
    {
        if (!IsInsideTree())
        {
            _pendingGeneration = () => GenerateMapWithWfc(seed, adjacencyRules, tileToTerrainType, abilityCards);
            return;
        }

        GD.Print($"[IrregularWorldMapScreen] Generating WFC map with seed {seed}");

        var generator = new MeshTerrainGenerator(adjacencyRules, tileToTerrainType);
        var mesh = generator.Generate(MeshRings, null, seed);

        var session = BeginSession(mesh);
        var playerStartCell = session.Prepare(seed, abilityCards?.ToList());
        CompleteSession(session, playerStartCell);

        StartExploration();
    }

    public void StartExploration() => _session?.StartExploration();
    public void StopExploration() => _session?.StopExploration();
    public void ToggleDebug()
    {
        ShowDebug = !ShowDebug;
        if (_debugRenderer != null) _debugRenderer.Visible = ShowDebug;
    }
    public void RevealAll() => _session?.RevealAll();

    public void Reset()
    {
        _session?.Reset();
        _isInitialized = false;
        _mesh = null;
        _mapData = null;
    }

    #endregion

    /// <summary>
    /// Creates the runtime for a freshly generated mesh and subscribes to its events.
    /// </summary>
    private WorldMapSession BeginSession(IrregularMesh mesh)
    {
        _session?.Close();
        _mesh = mesh;
        var session = new WorldMapSession(CreateSettings(), mesh);
        _session = session;
        _mapData = session.MapData;

        session.PlayerMoved += OnPlayerMoved;
        session.PositionUpdated += OnPositionUpdated;
        session.VisibilityChanged += changedCells => VisibilityChanged?.Invoke(changedCells);
        session.ExplorationFinished += () => ExplorationFinished?.Invoke();
        session.EnemySpotted += cellId => EnemySpotted?.Invoke(cellId);
        session.EnemyEncountered += cellId => EnemyEncountered?.Invoke(cellId);
        session.EnemyDefeated += cellId => EnemyDefeated?.Invoke(cellId);
        return session;
    }

    private void CompleteSession(WorldMapSession session, int playerStartCell)
    {
        session.Complete(playerStartCell);
        UpdatePlayerPosition(playerStartCell);

        _isInitialized = true;
        MapGenerated?.Invoke();
    }

    private WorldMapSettings CreateSettings()
    {
        return new WorldMapSettings
        {
            WorldScale = WorldScale,
            VisionRange = VisionRange,
            MovementSpeed = MovementSpeed,
            FogOfWarEnabled = FogOfWarEnabled,
            UseRaycastVisibility = UseRaycastVisibility,
            Viewport = Viewport,
            Camera = _camera2D,
            DebugRenderer = _debugRenderer,
            TileRegistry = _tileRegistry,
            Rng = _rng,
            CombatHost = this
        };
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
        _playerSprite.Texture = ColoredTextureFactory.Create(new Color(0, 0.8f, 0), 12, 12);
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

    private void UpdatePlayerPosition(int cellId)
    {
        if (_mapData == null || _playerSprite == null) return;
        _playerSprite.Position = _mapData.GetCellCenter(cellId);
    }

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
}
