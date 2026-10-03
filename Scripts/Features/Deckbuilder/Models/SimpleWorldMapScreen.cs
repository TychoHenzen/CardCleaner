using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;

/// <summary>
/// Enhanced world map screen with visual feedback for exploration and combat.
/// Renders 2D tile-based maps via SubViewport onto a 3D mesh for world-space display.
/// </summary>
[GlobalClass]
public partial class SimpleWorldMapScreen : Node3D
{
    // Visual constants - default, overridden by TilesetConfig when available
    private const int DefaultTileSize = 16;
    private int _tileSize = DefaultTileSize;

    // Default values as constants
    private const bool DefaultShowBiomeOverlay = true;

    private readonly List<Sprite2D> _enemySprites = new();
    private readonly HashSet<Vector2I> _renderedDebugTiles = new();

    private Camera2D? _camera2D;
    private IGameSessionService? _gameSession;
    private bool _hasLoggedTileInfo;
    private bool _isInitialized;

    private SimpleMapData? _mapData;
    private CardSignature[]? _pendingAbilities;
    private CardSignature[]? _pendingMapSeed;
    private bool _serviceReady;

    private ITileRegistry? _tileRegistry;
    private bool _usingCompiledAtlas;

    // Extracted components
    private WorldMapFogManager? _fogManager;
    private WorldMapCombatUI? _combatUI;
    private WorldMapTerrainRenderer? _terrainRenderer;
    private WorldMapBiomeOverlay? _biomeOverlay;

    // Biome preview support
    private BiomeRegistry? _biomeRegistry;
    private BiomeDistributionPreview? _biomePreview;

    #region Export Properties

    [Export] public TileMapLayer? TerrainLayer { get; set; }
    [Export] public TileMapLayer? DecorationLayer { get; set; }
    [Export] public TileMapLayer? StructureLayer { get; set; }
    [Export] public TileMapLayer? EffectLayer { get; set; }
    [Export] public TileMapLayer? OverlayLayer { get; set; }
    [Export] public TileMapLayer? BiomeOverlayLayer { get; set; }
    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    [Export] public Label? StatusLabel { get; set; }
    [Export] public Sprite2D? PlayerSprite { get; set; }
    [Export] public Control? CombatUI { get; set; }
    [Export] public bool ShowBiomeOverlay { get; set; } = DefaultShowBiomeOverlay;

    #endregion

    #region Property Revert Support

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(ShowBiomeOverlay) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(ShowBiomeOverlay) => DefaultShowBiomeOverlay,
            _ => base._PropertyGetRevert(property)
        };
    }

    #endregion

    #region Lifecycle

    public override void _Ready()
    {
        _combatUI = new WorldMapCombatUI(CombatUI);
        _combatUI.Hide();

        SetupBiomePreview();

        if (StatusLabel != null) StatusLabel.Text = "Waiting for map data...";

        CallDeferred(nameof(SetupScreenMesh));
        CallDeferred(nameof(SetupScreenMaterial));

        // Get tile registry for rendering and assign TileSet to layers
        ServiceLocator.Get<ITileRegistry>(registry =>
        {
            _tileRegistry = registry;
            _usingCompiledAtlas = registry.UsingCompiledAtlas && registry.CompiledTileSet != null;
            _tileSize = registry.TilesetConfig.BaseTileSize.X;

            ILog.Print($"[SimpleWorldMapScreen] TileRegistry: UsingCompiledAtlas={registry.UsingCompiledAtlas}, " +
                       $"TileSize={_tileSize}");

            // Initialize components that need tile registry
            InitializeComponents();

            // Assign TileSet to layers
            if (_usingCompiledAtlas)
            {
                AssignTileSetDirectly(registry.CompiledTileSet!);
            }
            else
            {
                AssignTileSetToLayers(registry.TilesetPath);
            }
        });

        // Use dependency injection to get the game session service
        ServiceLocator.Get<IGameSessionService>(gameSession =>
        {
            ILog.Print("Got GameSessionService from ServiceLocator");

            _gameSession = gameSession;
            _serviceReady = true;

            // Connect to all the events we need
            gameSession.StateChanged += OnSessionStateChanged;
            gameSession.MapGenerated += OnMapGenerated;
            gameSession.LootGenerated += OnLootGenerated;
            gameSession.PlayerMoved += OnServicePlayerMoved;
            gameSession.EnemyDefeated += OnServiceEnemyDefeated;
            gameSession.VisibilityUpdated += OnVisibilityUpdated;
            gameSession.PathUpdated += OnPathUpdated;

            // If Initialize() was called before service was ready, start now
            if (_pendingMapSeed is { Length: > 0 } && _pendingAbilities != null)
            {
                ILog.Print("Processing pending initialization...");
                gameSession.StartSession(_pendingMapSeed.ToList(), _pendingAbilities.ToList());
                _pendingMapSeed = null;
                _pendingAbilities = null;
            }
        });
    }

    public override void _ExitTree()
    {
        if (_gameSession != null)
        {
            _gameSession.StateChanged -= OnSessionStateChanged;
            _gameSession.MapGenerated -= OnMapGenerated;
            _gameSession.LootGenerated -= OnLootGenerated;
            _gameSession.PlayerMoved -= OnServicePlayerMoved;
            _gameSession.EnemyDefeated -= OnServiceEnemyDefeated;
            _gameSession.VisibilityUpdated -= OnVisibilityUpdated;
            _gameSession.PathUpdated -= OnPathUpdated;
        }
    }

    #endregion

    #region Public API

    public void Initialize(CardSignature[] mapSeed, CardSignature[] abilities)
    {
        if (mapSeed.Length == 0)
        {
            ILog.Error("Initialize called with empty mapSeed array");
            return;
        }

        ILog.Print(
            $"Initializing simple map screen with {mapSeed.Length} seed card(s) " +
            $"and {abilities.Length} abilities");

        if (_serviceReady && _gameSession != null)
        {
            _gameSession.StartSession(mapSeed.ToList(), abilities.ToList());
        }
        else
        {
            ILog.Print("GameSessionService not ready, storing pending initialization...");
            _pendingMapSeed = mapSeed;
            _pendingAbilities = abilities;
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Updates the biome distribution preview based on the given card signatures.
    /// Called by DeckBuilderController when MapCardSlot cards change.
    /// </summary>
    public void UpdateBiomePreview(CardSignature[] cards)
    {
        if (_biomePreview == null || _biomeRegistry == null) return;

        if (cards.Length == 0)
        {
            _biomePreview.Clear();
            return;
        }

        var distribution = BiomeDistributionCalculator.Calculate(cards, _biomeRegistry);
        _biomePreview.UpdateDistribution(distribution);
    }

    /// <summary>
    /// Resets the map screen to its initial state.
    /// </summary>
    public void ResetToInitialState()
    {
        ILog.Print("Resetting SimpleWorldMapScreen to initial state");

        // Clear all layers
        _terrainRenderer?.ClearAllLayers();
        OverlayLayer?.Clear();

        // Clear managed components
        _fogManager?.Clear();
        _biomeOverlay?.Clear();

        // Clear enemy sprites
        foreach (var sprite in _enemySprites)
            sprite?.QueueFree();
        _enemySprites.Clear();

        // Clear debug overlay tiles
        _renderedDebugTiles.Clear();

        // Hide player sprite
        if (PlayerSprite != null)
            PlayerSprite.Visible = false;

        // Hide combat UI
        _combatUI?.Hide();

        // Reset internal state
        _mapData = null;
        _isInitialized = false;
        _hasLoggedTileInfo = false;

        // Reset status label
        if (StatusLabel != null)
            StatusLabel.Text = "Waiting for map data...";

        // Show and clear biome preview
        if (_biomePreview != null)
        {
            _biomePreview.Visible = true;
            _biomePreview.Clear();
        }

        ConfigureCameraForPreview();

        ILog.Print("SimpleWorldMapScreen reset complete");
    }

    #endregion

    #region Initialization

    private void InitializeComponents()
    {
        if (Viewport == null) return;

        _fogManager = new WorldMapFogManager(Viewport, _tileSize);
        _biomeOverlay = new WorldMapBiomeOverlay(Viewport, _tileSize);
        _terrainRenderer = new WorldMapTerrainRenderer(
            TerrainLayer, DecorationLayer, StructureLayer, EffectLayer,
            _tileRegistry, _usingCompiledAtlas);
    }

    private void SetupBiomePreview()
    {
        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes();

        _biomePreview = new BiomeDistributionPreview();
        _biomePreview.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _biomePreview.OffsetLeft = 0;
        _biomePreview.OffsetTop = 0;
        _biomePreview.OffsetRight = 0;
        _biomePreview.OffsetBottom = 0;

        Viewport?.AddChild(_biomePreview);
        ConfigureCameraForPreview();
    }

    private void ConfigureCameraForPreview()
    {
        if (Viewport == null) return;

        _camera2D ??= Viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D == null) return;

        var viewportCenter = new Vector2(Viewport.Size.X / 2f, Viewport.Size.Y / 2f);
        _camera2D.GlobalPosition = viewportCenter;
        _camera2D.Zoom = Vector2.One;
        _camera2D.Enabled = true;
    }

    private void AssignTileSetToLayers(string tilesetPath)
    {
        var tileSet = GD.Load<TileSet>(tilesetPath);
        if (tileSet == null)
        {
            ILog.Error($"Failed to load TileSet from {tilesetPath}");
            return;
        }

        AssignTileSetDirectly(tileSet);
    }

    private void AssignTileSetDirectly(TileSet tileSet)
    {
        SetLayerTileSet(TerrainLayer, tileSet);
        SetLayerTileSet(DecorationLayer, tileSet);
        SetLayerTileSet(StructureLayer, tileSet);
        SetLayerTileSet(EffectLayer, tileSet);
        SetLayerTileSet(OverlayLayer, tileSet);
        SetLayerTileSet(BiomeOverlayLayer, tileSet);
    }

    private static void SetLayerTileSet(TileMapLayer? layer, TileSet? tileSet)
    {
        if (layer == null || tileSet == null) return;
        layer.TileSet = tileSet;
        layer.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
        layer.YSortEnabled = true;
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

    #region Event Handlers

    private void OnSessionStateChanged(SessionState newState)
    {
        if (StatusLabel != null) StatusLabel.Text = $"Status: {newState}";

        switch (newState)
        {
            case SessionState.GeneratingMap:
                HandleMapGeneration();
                break;
            case SessionState.Exploring:
                HandleExplorationStart();
                break;
            case SessionState.InCombat:
                HandleCombatStart();
                break;
            case SessionState.GeneratingLoot:
                HandleLootGeneration();
                break;
            case SessionState.SessionComplete:
                HandleSessionComplete();
                break;
        }
    }

    private void OnMapGenerated(SimpleMapData mapData)
    {
        ILog.Print($"Received map from GameSessionService: {mapData.Size.X}x{mapData.Size.Y}");
        _mapData = mapData;
        _renderedDebugTiles.Clear();
        RenderMap(_mapData);
        _fogManager?.Initialize(_mapData.Size);
        ILog.Print($"[FOG] Initialized fog of war with {_fogManager?.SpriteCount ?? 0} fog sprites");
    }

    private void OnServicePlayerMoved(Vector2I newPosition)
    {
        UpdatePlayerSpritePosition(newPosition);
    }

    private void OnPathUpdated(IReadOnlyList<Vector2I> path, Vector2I? target)
    {
        if (OverlayLayer == null || _terrainRenderer == null) return;

        // Clear previous debug overlay tiles
        foreach (var pos in _renderedDebugTiles)
            OverlayLayer.EraseCell(pos);
        _renderedDebugTiles.Clear();

        // Render new path tiles
        var (pathSourceId, pathAtlasCoords) = _terrainRenderer.GetTileRenderInfo("debug_path");
        foreach (var pos in path)
        {
            OverlayLayer.SetCell(pos, pathSourceId, pathAtlasCoords);
            _renderedDebugTiles.Add(pos);
        }

        // Render target tile
        if (target.HasValue)
        {
            var (targetSourceId, targetAtlasCoords) = _terrainRenderer.GetTileRenderInfo("debug_target");
            OverlayLayer.SetCell(target.Value, targetSourceId, targetAtlasCoords);
            _renderedDebugTiles.Add(target.Value);
        }
    }

    private void OnVisibilityUpdated(IReadOnlySet<Vector2I> seenTiles, IReadOnlySet<Vector2I> currentlyVisibleTiles)
    {
        _fogManager?.UpdateVisibility(seenTiles, currentlyVisibleTiles);
    }

    private void OnServiceEnemyDefeated(Vector2I position)
    {
        ILog.Print($"Enemy defeated at {position} - removing sprite");

        var spriteToRemove = _enemySprites.FirstOrDefault(sprite =>
        {
            var spriteGridPos = new Vector2I(
                Mathf.RoundToInt((sprite.Position.X - _tileSize / 2f) / _tileSize),
                Mathf.RoundToInt((sprite.Position.Y - _tileSize / 2f) / _tileSize)
            );
            return spriteGridPos == position;
        });

        if (spriteToRemove != null)
        {
            _enemySprites.Remove(spriteToRemove);
            spriteToRemove.QueueFree();
        }
    }

    private void OnLootGenerated(List<CardSignature> lootSignatures)
    {
        ILog.Print($"Loot generated: {lootSignatures.Count} cards");
        SpawnLootCards(lootSignatures);
    }

    #endregion

    #region State Handlers

    private void HandleMapGeneration()
    {
        if (StatusLabel != null) StatusLabel.Text = "Generating map...";
        if (_biomePreview != null) _biomePreview.Visible = false;
    }

    private void HandleExplorationStart()
    {
        if (StatusLabel != null) StatusLabel.Text = "Exploring map...";

        if (PlayerSprite != null)
        {
            PlayerSprite.Visible = true;
            PlayerSprite.ZIndex = 200;
        }
    }

    private void HandleCombatStart()
    {
        if (StatusLabel != null) StatusLabel.Text = "Combat started!";
        _combatUI?.Show();

        if (_gameSession != null)
            StartCombatVisualization();
    }

    private void HandleLootGeneration()
    {
        if (StatusLabel != null) StatusLabel.Text = "Generating loot...";
        _fogManager?.RevealAll();
        ILog.Print("[FOG] Revealed entire map");
    }

    private void HandleSessionComplete()
    {
        if (StatusLabel != null) StatusLabel.Text = "Session complete!";
        ILog.Print("Session complete - ready for next round!");
    }

    #endregion

    #region Combat

    private void StartCombatVisualization()
    {
        var timer = new Timer();
        AddChild(timer);
        timer.WaitTime = 1.0f;

        var playerHealth = 50;
        var enemyHealth = 30;
        var playerMaxHealth = 50;
        var enemyMaxHealth = 30;

        timer.Timeout += () =>
        {
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            if (rng.Randf() > 0.5f)
            {
                enemyHealth -= rng.RandiRange(8, 15);
                _combatUI?.UpdateAction("Player attacks!");
            }
            else
            {
                playerHealth -= rng.RandiRange(5, 10);
                _combatUI?.UpdateAction("Enemy attacks!");
            }

            _combatUI?.UpdateHealth(playerHealth, playerMaxHealth, enemyHealth, enemyMaxHealth);

            if (enemyHealth <= 0)
            {
                _combatUI?.UpdateAction("Victory!");
                timer.QueueFree();
                CallDeferred(MethodName.EndCombat, true);
            }
            else if (playerHealth <= 0)
            {
                _combatUI?.UpdateAction("Defeat!");
                timer.QueueFree();
                CallDeferred(MethodName.EndCombat, false);
            }
        };
        timer.Start();
    }

    private void EndCombat(bool playerWon)
    {
        _combatUI?.Hide();

        if (StatusLabel != null)
            StatusLabel.Text = playerWon ? "Victory! Generating loot..." : "Defeat!";
    }

    #endregion

    #region Rendering

    private void RenderMap(SimpleMapData mapData)
    {
        if (TerrainLayer == null || _terrainRenderer == null)
        {
            ILog.Error("RenderMap: TerrainLayer or TerrainRenderer is null!");
            return;
        }

        // Clear all layers and old sprites
        _terrainRenderer.ClearAllLayers();
        OverlayLayer?.Clear();

        foreach (var sprite in _enemySprites) sprite?.QueueFree();
        _enemySprites.Clear();

        // Resize viewport to fit the map
        SetupViewport(mapData.Size);

        // Log tile info for debugging
        _terrainRenderer.LogTileRenderingSample(mapData);

        // Render terrain and non-terrain tiles
        _terrainRenderer.RenderTerrainTransitions(mapData);
        _terrainRenderer.RenderNonTerrainTiles(mapData);

        // Render biome overlay if enabled
        if (ShowBiomeOverlay)
            _biomeOverlay?.Render(mapData);

        // Create enemy sprites
        CreateEnemySprites(mapData.EnemyPositions);

        // Configure camera after map is rendered
        CallDeferred(nameof(ConfigureCamera));

        ILog.Print($"Rendered map: {mapData.Size.X}x{mapData.Size.Y} with {mapData.EnemyPositions.Count} enemies");
    }

    private void SetupViewport(Vector2I mapSize)
    {
        if (Viewport == null) return;

        Viewport.Size = new Vector2I((mapSize.X + 1) * _tileSize, (mapSize.Y + 1) * _tileSize);
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;

        _camera2D = Viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D != null) _camera2D.Enabled = true;
    }

    private void ConfigureCamera()
    {
        if (_camera2D == null || TerrainLayer == null || Viewport == null) return;

        var usedRect = TerrainLayer.GetUsedRect();
        if (usedRect.Size == Vector2I.Zero) return;

        var mapCenter = new Vector2(
            (usedRect.Position.X * _tileSize) + (usedRect.Size.X * _tileSize / 2f),
            (usedRect.Position.Y * _tileSize) + (usedRect.Size.Y * _tileSize / 2f)
        );
        _camera2D.GlobalPosition = mapCenter;

        var mapPixelSize = new Vector2(usedRect.Size.X * _tileSize, usedRect.Size.Y * _tileSize);
        var viewportSize = Viewport.Size;

        var zoomX = viewportSize.X / mapPixelSize.X;
        var zoomY = viewportSize.Y / mapPixelSize.Y;
        var zoom = Mathf.Min(zoomX, zoomY);

        _camera2D.Zoom = new Vector2(zoom, zoom);
    }

    private void CreateEnemySprites(List<Vector2I> enemyPositions)
    {
        foreach (var pos in enemyPositions)
        {
            var enemySprite = new Sprite2D();
            enemySprite.Texture = CreateColorTexture(Colors.Red, _tileSize);
            enemySprite.Position = new Vector2(pos.X * _tileSize + _tileSize / 2, pos.Y * _tileSize + _tileSize / 2);
            enemySprite.ZIndex = 200;

            Viewport?.AddChild(enemySprite);
            _enemySprites.Add(enemySprite);
        }
    }

    private void UpdatePlayerSpritePosition(Vector2I gridPosition)
    {
        if (PlayerSprite != null)
            PlayerSprite.Position = new Vector2(
                gridPosition.X * _tileSize + _tileSize / 2,
                gridPosition.Y * _tileSize + _tileSize / 2
            );
    }

    private void SpawnLootCards(List<CardSignature> lootSignatures)
    {
        ServiceLocator.Get<ICardSpawningService>(spawningService =>
        {
            for (var i = 0; i < lootSignatures.Count; i++)
            {
                var spawnPos = new Vector3(i * 0.5f, 1.5f, 2f);

                ServiceLocator.Get<ICardSpawner>(spawner =>
                {
                    var spawnTransform = Transform3D.Identity;
                    spawnTransform.Origin = spawnPos;
                    spawningService.SpawnCard(lootSignatures[i], spawnTransform, spawner.GetNode());
                });
            }

            ILog.Print($"Spawned {lootSignatures.Count} loot cards!");
        });
    }

    private static ImageTexture CreateColorTexture(Color color, int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    #endregion
}
