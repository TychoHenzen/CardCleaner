using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
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
    // Counts generation requests; a generation whose number is no longer current (a newer request or a reset came
    // in while it ran) is dropped.
    private int _generationRequest;
    private RandomNumberGenerator _rng = new();

    // Tile registry the render layer reads tile art from
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
    /// Generates a new map with the given seed through the irregular map generator, off the main thread, then shows
    /// it and starts exploring from the start the generator placed. A screen not yet in the tree generates once it
    /// is ready. The task completes when the map is shown, or when a newer request or a reset dropped it. A generator
    /// that returns no mesh map throws, so the caller ends the session.
    /// </summary>
    public async Task GenerateMap(
        IMeshMapGenerator generator,
        int seed,
        CardSignature[]? inputCards = null,
        CardSignature[]? abilityCards = null)
    {
        ArgumentNullException.ThrowIfNull(generator);

        if (!IsInsideTree())
            await ToSignal(this, Node.SignalName.Ready);

        var request = ++_generationRequest;
        _rng.Seed = (ulong)seed;
        // Close the running session now, not when the new map arrives: left running during the off-thread generation,
        // its finish would end the new session early.
        _session?.Close();
        _session = null;
        _isInitialized = false;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        GD.Print($"[IrregularWorldMapScreen] Generating map with seed {seed}, rings={MeshRings}");

        var generated = await generator.GenerateAsync(new MeshMapGenerationConfig
        {
            Rings = MeshRings,
            Seed = unchecked((ulong)seed),
            MapSeeds = inputCards ?? Array.Empty<CardSignature>()
        });

        if (!IsInstanceValid(this) || request != _generationRequest || !IsInsideTree())
            return;

        if (generated is not IMeshGeneratedMap meshMap)
            throw new InvalidOperationException("The generator returned no irregular mesh map");

        GD.Print($"[IrregularWorldMapScreen] Map generation complete ({sw.ElapsedMilliseconds}ms)");

        var session = BeginSession(meshMap);
        var playerStartCell = session.Prepare(abilityCards?.ToList());
        GD.Print($"[IrregularWorldMapScreen] Rendering setup complete ({sw.ElapsedMilliseconds}ms total)");

        CompleteSession(session, playerStartCell);

        sw.Stop();
        GD.Print(
            $"[IrregularWorldMapScreen] Map generated in {sw.ElapsedMilliseconds}ms: " +
            $"{meshMap.RawMesh.GetStatistics()}");

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

    /// <summary>
    /// Names the picked map kind in this screen's status label. The explorer's position text replaces it while a
    /// session runs; the cabinet shows the kind again after the reset press.
    /// </summary>
    public void ShowMapKind(string kindText)
    {
        if (StatusLabel != null) StatusLabel.Text = kindText;
    }

    public void Reset()
    {
        _generationRequest++;
        _session?.Reset();
        _isInitialized = false;
        _mesh = null;
        _mapData = null;
    }

    #endregion

    /// <summary>
    /// Creates the runtime for a freshly generated map and subscribes to its events.
    /// </summary>
    private WorldMapSession BeginSession(IMeshGeneratedMap map)
    {
        _session?.Close();
        _mesh = map.RawMesh;
        var session = new WorldMapSession(CreateSettings(map.MeshMapData.WorldScale), map);
        _session = session;
        _mapData = session.MapData;

        session.PlayerMoved += OnPlayerMoved;
        session.PositionUpdated += OnPositionUpdated;
        session.VisibilityChanged += changedCells => VisibilityChanged?.Invoke(changedCells);
        session.ExplorationFinished += RaiseExplorationFinished;
        session.EnemySpotted += cellId => EnemySpotted?.Invoke(cellId);
        session.EnemyEncountered += cellId => EnemyEncountered?.Invoke(cellId);
        session.EnemyDefeated += cellId => EnemyDefeated?.Invoke(cellId);
        return session;
    }

    /// <summary>Raises <see cref="ExplorationFinished"/>. Internal so tests can finish a session without exploring it.</summary>
    internal void RaiseExplorationFinished()
    {
        ExplorationFinished?.Invoke();
    }

    private void CompleteSession(WorldMapSession session, int playerStartCell)
    {
        session.Complete(playerStartCell);
        UpdatePlayerPosition(playerStartCell);

        _isInitialized = true;
        MapGenerated?.Invoke();
    }

    private WorldMapSettings CreateSettings(float worldScale)
    {
        return new WorldMapSettings
        {
            WorldScale = worldScale,
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
