using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Enumeration;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;
using Godot.Collections;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Enhanced world map screen with visual feedback for exploration and combat
/// </summary>
[GlobalClass]
public partial class SimpleWorldMapScreen : Node3D
{
    // Visual constants
    private const int TILE_SIZE = 16;

    // Default values as constants
    private const bool DefaultShowBiomeOverlay = true;

    // Biome colors for overlay visualization (semi-transparent)
    private static readonly System.Collections.Generic.Dictionary<string, Color> BiomeColors = new()
    {
        { "plains", new Color(0.3f, 0.8f, 0.3f, 0.4f) }, // Green
        { "forest", new Color(0.1f, 0.5f, 0.1f, 0.4f) }, // Dark Green
        { "desert", new Color(0.9f, 0.8f, 0.4f, 0.4f) }, // Sandy Yellow
        { "tundra", new Color(0.7f, 0.9f, 1.0f, 0.4f) }, // Ice Blue
        { "mountains", new Color(0.5f, 0.5f, 0.5f, 0.4f) }, // Grey
        { "swamp", new Color(0.3f, 0.4f, 0.2f, 0.4f) } // Murky Green
    };

    private readonly List<Sprite2D> _biomeOverlaySprites = new();

    // Cache for biome overlay textures
    private readonly System.Collections.Generic.Dictionary<string, ImageTexture> _biomeTextures = new();
    private readonly List<Sprite2D> _enemySprites = new();

    // Fog of war system
    private readonly System.Collections.Generic.Dictionary<Vector2I, Sprite2D> _fogSprites = new();
    private readonly HashSet<Vector2I> _renderedDebugTiles = new();
    private Label? _actionLabel;
    private Camera2D? _camera2D;
    private ProgressBar? _enemyHealthBar;
    private Label? _enemyHealthLabel;
    private ImageTexture? _fogTexture;
    private IGameSessionService? _gameSession;

    private bool _hasLoggedTileInfo;
    private bool _isInitialized;

    private SimpleMapData? _mapData;
    private CardSignature[]? _pendingAbilities;

    // Pending initialization data (stored if Initialize called before service ready)
    private CardSignature[]? _pendingMapSeed;

    // Combat UI elements
    private ProgressBar? _playerHealthBar;
    private Label? _playerHealthLabel;
    private bool _serviceReady;

    private ITileRegistry? _tileRegistry;

    // Transition resolver for compiled atlas lookups
    private ITransitionResolver? _transitionResolver;
    private bool _usingCompiledAtlas;

    // Biome preview support
    private BiomeRegistry? _biomeRegistry;
    private BiomeDistributionPreview? _biomePreview;

    // Export properties for editor assignment - multiple layers for proper rendering order
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

    public override void _Ready()
    {
        SetupCombatUIReferences();
        SetupBiomePreview();

        if (StatusLabel != null) StatusLabel.Text = "Waiting for map data...";

        if (CombatUI != null) CombatUI.Visible = false;

        CallDeferred(nameof(SetupScreenMesh));
        CallDeferred(nameof(SetupScreenMaterial));

        // Get tile registry for rendering and assign TileSet to layers
        ServiceLocator.Get<ITileRegistry>(registry =>
        {
            _tileRegistry = registry;
            _usingCompiledAtlas = registry.UsingCompiledAtlas && registry.CompiledTileSet != null;

            ILog.Print($"[SimpleWorldMapScreen] TileRegistry: UsingCompiledAtlas={registry.UsingCompiledAtlas}, " +
                       $"CompiledTileSet={(registry.CompiledTileSet != null ? "present" : "NULL")}, " +
                       $"TilesetPath={registry.TilesetPath}, _usingCompiledAtlas={_usingCompiledAtlas}");

            // Use compiled TileSet directly if available, otherwise load from path
            if (_usingCompiledAtlas)
            {
                ILog.Print("[SimpleWorldMapScreen] Using COMPILED TileSet path");
                AssignTileSetDirectly(registry.CompiledTileSet!);
            }
            else
            {
                ILog.Print("[SimpleWorldMapScreen] Using FALLBACK TileSet path - transition resolver will be disabled");
                AssignTileSetToLayers(registry.TilesetPath);
            }
        });

        // Use dependency injection to get the game session service
        ServiceLocator.Get<IGameSessionService>(gameSession =>
        {
            ILog.Print("Got GameSessionService from ServiceLocator");

            // Store the interface reference
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
        // Clean up event subscriptions
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

    public void Initialize(CardSignature[] mapSeed, CardSignature[] abilities)
    {
        if (mapSeed.Length == 0)
        {
            ILog.Error("Initialize called with empty mapSeed array");
            return;
        }

        ILog.Print(
            $"Initializing simple map screen with {mapSeed.Length} seed card(s) and {abilities.Length} abilities");
        ILog.Print(
            $"CardSignature Elements: [{string.Join(", ", mapSeed[0].Elements.Select(e => e.ToString("F3")))}]");

        if (_serviceReady && _gameSession != null)
        {
            // Service is ready, start immediately
            _gameSession.StartSession(mapSeed.ToList(), abilities.ToList());
        }
        else
        {
            // Service not ready yet, store for when callback fires
            ILog.Print("GameSessionService not ready, storing pending initialization...");
            _pendingMapSeed = mapSeed;
            _pendingAbilities = abilities;
        }

        _isInitialized = true;
    }

    private void SetupCombatUIReferences()
    {
        _playerHealthBar = CombatUI?.GetNode<ProgressBar>("PlayerHealthBar");
        _enemyHealthBar = CombatUI?.GetNode<ProgressBar>("EnemyHealthBar");
        _playerHealthLabel = CombatUI?.GetNode<Label>("PlayerHealthLabel");
        _enemyHealthLabel = CombatUI?.GetNode<Label>("EnemyHealthLabel");
        _actionLabel = CombatUI?.GetNode<Label>("ActionLabel");
    }

    /// <summary>
    /// Loads the TileSet at runtime and assigns it to all TileMapLayer nodes.
    /// This avoids loading the massive TileSet at scene parse time.
    /// </summary>
    private void AssignTileSetToLayers(string tilesetPath)
    {
        var tileSet = GD.Load<TileSet>(tilesetPath);
        if (tileSet == null)
        {
            ILog.Error($"Failed to load TileSet from {tilesetPath}");
            return;
        }

        AssignTileSetDirectly(tileSet);
        ILog.Print($"[SimpleWorldMapScreen] Assigned TileSet from {tilesetPath} to all layers");
    }

    /// <summary>
    /// Assigns a TileSet directly to all TileMapLayer nodes.
    /// Used when TileSet is already loaded (e.g., from compiled atlas).
    /// </summary>
    private void AssignTileSetDirectly(TileSet tileSet)
    {
        // Log TileSet info for debugging
        var sourceCount = tileSet.GetSourceCount();
        ILog.Print($"[SimpleWorldMapScreen] Assigning TileSet with {sourceCount} source(s)");
        for (var i = 0; i < sourceCount; i++)
        {
            var sourceId = tileSet.GetSourceId(i);
            var source = tileSet.GetSource(sourceId);
            var sourceType = source?.GetType().Name ?? "null";
            ILog.Print($"[SimpleWorldMapScreen]   Source {sourceId}: {sourceType}");
        }

        // Assign TileSet and ensure nearest-neighbor filtering for pixel art
        SetLayerTileSet(TerrainLayer, tileSet);
        SetLayerTileSet(DecorationLayer, tileSet);
        SetLayerTileSet(StructureLayer, tileSet);
        SetLayerTileSet(EffectLayer, tileSet);
        SetLayerTileSet(OverlayLayer, tileSet);
        SetLayerTileSet(BiomeOverlayLayer, tileSet);

        ILog.Print($"[SimpleWorldMapScreen] Assigned TileSet to all 6 layers");
    }

    private static void SetLayerTileSet(TileMapLayer? layer, TileSet? tileSet)
    {
        if (layer == null || tileSet == null) return;
        layer.TileSet = tileSet;
        layer.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
    }

    private void SetupBiomePreview()
    {
        // Initialize biome registry with default biomes for preview calculation
        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes();

        // Create and add the preview control to the viewport (fills entire viewport)
        _biomePreview = new BiomeDistributionPreview();
        _biomePreview.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _biomePreview.OffsetLeft = 0;
        _biomePreview.OffsetTop = 0;
        _biomePreview.OffsetRight = 0;
        _biomePreview.OffsetBottom = 0;

        Viewport?.AddChild(_biomePreview);

        // Configure camera for initial preview state
        ConfigureCameraForPreview();
    }

    /// <summary>
    /// Configures the camera to display the biome preview correctly.
    /// Centers the camera on the viewport and resets zoom.
    /// </summary>
    private void ConfigureCameraForPreview()
    {
        if (Viewport == null) return;

        _camera2D ??= Viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D == null) return;

        // Center camera on the viewport area so Controls appear correctly
        var viewportCenter = new Vector2(Viewport.Size.X / 2f, Viewport.Size.Y / 2f);
        _camera2D.GlobalPosition = viewportCenter;
        _camera2D.Zoom = Vector2.One;
        _camera2D.Enabled = true;
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

    private void HandleMapGeneration()
    {
        if (StatusLabel != null) StatusLabel.Text = "Generating map...";

        // Hide biome preview when generating map
        if (_biomePreview != null) _biomePreview.Visible = false;

        // Map will be received via OnMapGenerated event
    }

    private void OnMapGenerated(SimpleMapData mapData)
    {
        ILog.Print($"Received map from GameSessionService: {mapData.Size.X}x{mapData.Size.Y}");
        _mapData = mapData;
        _renderedDebugTiles.Clear();
        RenderMap(_mapData);
        InitializeFogOfWar(_mapData);
    }

    private void HandleExplorationStart()
    {
        if (StatusLabel != null) StatusLabel.Text = "Exploring map...";

        // Just show the player sprite - GameSessionService handles the exploration logic
        // and sends us PlayerMoved events
        if (PlayerSprite != null)
        {
            PlayerSprite.Visible = true;
            PlayerSprite.ZIndex = 200; // Above fog of war
        }
    }

    private void OnServicePlayerMoved(Vector2I newPosition)
    {
        UpdatePlayerSpritePosition(newPosition);
    }

    private void OnPathUpdated(IReadOnlyList<Vector2I> path, Vector2I? target)
    {
        if (OverlayLayer == null) return;

        // Clear previous debug overlay tiles
        foreach (var pos in _renderedDebugTiles)
            OverlayLayer.EraseCell(pos);

        _renderedDebugTiles.Clear();

        // Render new path tiles
        var (pathSourceId, pathAtlasCoords) = GetTileRenderInfo("debug_path");
        foreach (var pos in path)
        {
            OverlayLayer.SetCell(pos, pathSourceId, pathAtlasCoords);
            _renderedDebugTiles.Add(pos);
        }

        // Render target tile (overwrites path tile if on same position)
        if (target.HasValue)
        {
            var (targetSourceId, targetAtlasCoords) = GetTileRenderInfo("debug_target");
            OverlayLayer.SetCell(target.Value, targetSourceId, targetAtlasCoords);
            _renderedDebugTiles.Add(target.Value);
        }
    }

    private void OnVisibilityUpdated(IReadOnlySet<Vector2I> seenTiles, IReadOnlySet<Vector2I> currentlyVisibleTiles)
    {
        UpdateFogOfWar(seenTiles, currentlyVisibleTiles);
    }

    private void InitializeFogOfWar(SimpleMapData mapData)
    {
        // Clear existing fog sprites
        foreach (var sprite in _fogSprites.Values)
            sprite?.QueueFree();
        _fogSprites.Clear();

        // Create black fog texture if not already created
        _fogTexture ??= CreateColorTexture(new Color(0, 0, 0, 1), TILE_SIZE);

        // Create fog sprites for all tiles (90% opacity so map is barely visible)
        for (var y = 0; y < mapData.Size.Y; y++)
        for (var x = 0; x < mapData.Size.X; x++)
        {
            var position = new Vector2I(x, y);
            var sprite = new Sprite2D
            {
                Texture = _fogTexture,
                Position = new Vector2(x * TILE_SIZE + TILE_SIZE / 2, y * TILE_SIZE + TILE_SIZE / 2),
                ZIndex = 50, // Below UI (which uses CanvasLayer)
                Modulate = new Color(1, 1, 1, 0.9f) // 90% opacity - map barely visible through fog
            };

            Viewport?.AddChild(sprite);
            _fogSprites[position] = sprite;
        }

        ILog.Print($"[FOG] Initialized fog of war with {_fogSprites.Count} fog sprites");
    }

    private void UpdateFogOfWar(IReadOnlySet<Vector2I> seenTiles, IReadOnlySet<Vector2I> currentlyVisibleTiles)
    {
        foreach (var (position, sprite) in _fogSprites)
        {
            if (currentlyVisibleTiles.Contains(position))
            {
                // Currently visible: fully transparent (no fog)
                sprite.Modulate = new Color(1, 1, 1, 0);
            }
            else if (seenTiles.Contains(position))
            {
                // Previously seen but not currently visible: 50% fog
                sprite.Modulate = new Color(1, 1, 1, 0.5f);
            }
            // else: Never seen - stays at 90% opacity (set in InitializeFogOfWar)
        }
    }

    private void OnServiceEnemyDefeated(Vector2I position)
    {
        ILog.Print($"Enemy defeated at {position} - removing sprite");

        // Find and remove the enemy sprite at this position
        var spriteToRemove = _enemySprites.FirstOrDefault(sprite =>
        {
            var spriteGridPos = new Vector2I(
                Mathf.RoundToInt((sprite.Position.X - TILE_SIZE / 2) / TILE_SIZE),
                Mathf.RoundToInt((sprite.Position.Y - TILE_SIZE / 2) / TILE_SIZE)
            );
            return spriteGridPos == position;
        });

        if (spriteToRemove != null)
        {
            _enemySprites.Remove(spriteToRemove);
            spriteToRemove.QueueFree();
            ILog.Print($"Removed enemy sprite at {position}");
        }
    }

    private void HandleCombatStart()
    {
        if (StatusLabel != null) StatusLabel.Text = "Combat started!";

        if (CombatUI != null) CombatUI.Visible = true;

        // Connect to combat system if available
        if (_gameSession != null)
            // In the real implementation, we'd get combat updates from the session service
            StartCombatVisualization();
    }

    private void StartCombatVisualization()
    {
        // Create a timer to update combat visuals
        var timer = new Timer();
        AddChild(timer);
        timer.WaitTime = 1.0f;

        // Simulate combat progress
        var playerHealth = 50;
        var enemyHealth = 30;
        var playerMaxHealth = 50;
        var enemyMaxHealth = 30;

        timer.Timeout += () =>
        {
            // Simulate combat damage
            var rng = new RandomNumberGenerator();
            rng.Randomize();

            if (rng.Randf() > 0.5f)
            {
                enemyHealth -= rng.RandiRange(8, 15);
                UpdateActionText("Player attacks!");
            }
            else
            {
                playerHealth -= rng.RandiRange(5, 10);
                UpdateActionText("Enemy attacks!");
            }

            UpdateCombatUI(playerHealth, playerMaxHealth, enemyHealth, enemyMaxHealth);

            if (enemyHealth <= 0)
            {
                UpdateActionText("Victory!");
                timer.QueueFree();
                CallDeferred(MethodName.EndCombat, true);
            }
            else if (playerHealth <= 0)
            {
                UpdateActionText("Defeat!");
                timer.QueueFree();
                CallDeferred(MethodName.EndCombat, false);
            }
        };
        timer.Start();
    }

    private void UpdateCombatUI(int playerHealth, int playerMaxHealth, int enemyHealth, int enemyMaxHealth)
    {
        if (_playerHealthBar != null) _playerHealthBar.Value = (float)playerHealth / playerMaxHealth * 100f;

        if (_enemyHealthBar != null) _enemyHealthBar.Value = (float)enemyHealth / enemyMaxHealth * 100f;

        if (_playerHealthLabel != null)
            _playerHealthLabel.Text = $"Player: {Mathf.Max(0, playerHealth)}/{playerMaxHealth}";

        if (_enemyHealthLabel != null) _enemyHealthLabel.Text = $"Enemy: {Mathf.Max(0, enemyHealth)}/{enemyMaxHealth}";
    }

    private void UpdateActionText(string action)
    {
        if (_actionLabel != null) _actionLabel.Text = action;
    }

    private void EndCombat(bool playerWon)
    {
        if (CombatUI != null) CombatUI.Visible = false;

        if (playerWon)
        {
            if (StatusLabel != null) StatusLabel.Text = "Victory! Generating loot...";
        }
        else
        {
            if (StatusLabel != null) StatusLabel.Text = "Defeat!";
        }
    }

    private void HandleLootGeneration()
    {
        if (StatusLabel != null) StatusLabel.Text = "Generating loot...";

        RevealEntireMap();
    }

    private void RevealEntireMap()
    {
        foreach (var sprite in _fogSprites.Values)
            sprite.Modulate = new Color(1, 1, 1, 0);

        ILog.Print("[FOG] Revealed entire map");
    }

    private void OnLootGenerated(List<CardSignature> lootSignatures)
    {
        ILog.Print($"Loot generated: {lootSignatures.Count} cards");
        SpawnLootCards(lootSignatures);
    }

    private void SpawnLootCards(List<CardSignature> lootSignatures)
    {
        // Use dependency injection to get the card spawning service
        ServiceLocator.Get<ICardSpawningService>(spawningService =>
        {
            for (var i = 0; i < lootSignatures.Count; i++)
            {
                var spawnPos = new Vector3(i * 0.5f, 1.5f, 2f); // Safe altitude

                // Get the card spawner node
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

    private void HandleSessionComplete()
    {
        if (StatusLabel != null) StatusLabel.Text = "Session complete!";

        ILog.Print("Session complete - ready for next round!");
    }

    /// <summary>
    /// Resets the map screen to its initial state, clearing all visual elements
    /// and preparing it to show the biome preview again.
    /// Called by DeckBuilderController when starting a new session.
    /// </summary>
    public void ResetToInitialState()
    {
        ILog.Print("Resetting SimpleWorldMapScreen to initial state");

        // Clear all tile layers
        TerrainLayer?.Clear();
        DecorationLayer?.Clear();
        StructureLayer?.Clear();
        EffectLayer?.Clear();
        OverlayLayer?.Clear();

        // Clear fog sprites
        foreach (var sprite in _fogSprites.Values)
            sprite?.QueueFree();
        _fogSprites.Clear();

        // Clear enemy sprites
        foreach (var sprite in _enemySprites)
            sprite?.QueueFree();
        _enemySprites.Clear();

        // Clear biome overlay sprites
        foreach (var sprite in _biomeOverlaySprites)
            sprite?.QueueFree();
        _biomeOverlaySprites.Clear();

        // Clear debug overlay tiles
        _renderedDebugTiles.Clear();

        // Hide player sprite
        if (PlayerSprite != null)
            PlayerSprite.Visible = false;

        // Hide combat UI
        if (CombatUI != null)
            CombatUI.Visible = false;

        // Reset internal state
        _mapData = null;
        _isInitialized = false;
        _hasLoggedTileInfo = false;

        // Reset status label
        if (StatusLabel != null)
            StatusLabel.Text = "Waiting for map data...";

        // Show and clear biome preview (will be updated when cards are added)
        if (_biomePreview != null)
        {
            _biomePreview.Visible = true;
            _biomePreview.Clear();
        }

        // Reconfigure camera for preview display
        ConfigureCameraForPreview();

        ILog.Print("SimpleWorldMapScreen reset complete");
    }

    private void RenderMap(SimpleMapData mapData)
    {
        if (TerrainLayer == null)
        {
            ILog.Error("RenderMap: TerrainLayer is null!");
            return;
        }

        // Clear all layers
        TerrainLayer.Clear();
        DecorationLayer?.Clear();
        StructureLayer?.Clear();
        EffectLayer?.Clear();
        OverlayLayer?.Clear();

        // Clear old enemy sprites
        foreach (var sprite in _enemySprites) sprite?.QueueFree();
        _enemySprites.Clear();

        // Resize viewport to fit the map
        SetupViewport(mapData.Size);

        // Log tile info for debugging
        LogTileRenderingSample();

        // Render dual-grid terrain (base + top layers with half-tile offset)
        // This handles ALL terrain rendering using the visual grid
        RenderTerrainTransitions(mapData);

        // Render non-terrain tiles (structures, decorations, effects) from data grid
        // These render at data grid positions (no offset) on top of the dual-grid terrain
        RenderNonTerrainTiles(mapData);

        // Render biome overlay if enabled
        if (ShowBiomeOverlay) RenderBiomeOverlay(mapData);

        // Create enemy sprites
        CreateEnemySprites(mapData.EnemyPositions);

        // Configure camera after map is rendered (deferred to ensure GetUsedRect works)
        CallDeferred(nameof(ConfigureCamera));

        ILog.Print($"Rendered map: {mapData.Size.X}x{mapData.Size.Y} with {mapData.EnemyPositions.Count} enemies");
    }

    private void RenderBiomeOverlay(SimpleMapData mapData)
    {
        // Clear previous biome overlay sprites
        foreach (var sprite in _biomeOverlaySprites)
            sprite?.QueueFree();
        _biomeOverlaySprites.Clear();

        if (mapData.BiomeMap == null)
        {
            ILog.Print("[BIOME OVERLAY] No biome data available");
            return;
        }

        // Count biomes for logging
        var biomeCounts = new System.Collections.Generic.Dictionary<string, int>();

        // Create textures for each biome type if not cached
        foreach (var (biomeId, color) in BiomeColors)
            if (!_biomeTextures.ContainsKey(biomeId))
                _biomeTextures[biomeId] = CreateColorTexture(color, TILE_SIZE);

        // Create sprites for each tile position showing biome color
        for (var y = 0; y < mapData.Size.Y; y++)
        for (var x = 0; x < mapData.Size.X; x++)
        {
            var biomeId = mapData.GetBiomeAt(new Vector2I(x, y));

            // Track counts
            biomeCounts.TryGetValue(biomeId, out var count);
            biomeCounts[biomeId] = count + 1;

            // Get or create texture for this biome
            if (!_biomeTextures.TryGetValue(biomeId, out var texture))
            {
                texture = CreateColorTexture(new Color(1, 0, 1, 0.5f), TILE_SIZE); // Magenta fallback
                _biomeTextures[biomeId] = texture;
            }

            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = new Vector2(x * TILE_SIZE + TILE_SIZE / 2, y * TILE_SIZE + TILE_SIZE / 2),
                ZIndex = 10 // Above tiles but below player/enemies
            };

            Viewport?.AddChild(sprite);
            _biomeOverlaySprites.Add(sprite);
        }

        // Log biome distribution
        ILog.Print("[BIOME OVERLAY] Biome distribution from map data:");
        var totalTiles = mapData.Size.X * mapData.Size.Y;
        foreach (var (biomeId, count) in biomeCounts)
        {
            var percentage = (float)count / totalTiles * 100;
            ILog.Print($"  {biomeId}: {count} tiles ({percentage:F1}%)");
        }
    }

    /// <summary>
    /// Render dual-grid terrain: visual grid at standard positions, each tile composited from 4 data corners.
    /// Uses the dual-tilemap technique from https://excaliburjs.com/blog/Dual%20Tilemap%20Autotiling%20Technique/
    /// Visual grid is (size+1) x (size+1), each visual tile samples 4 data corners to compute bitmask.
    /// Pre-composited transition tiles are looked up from the compiled atlas.
    /// </summary>
    private void RenderTerrainTransitions(SimpleMapData mapData)
    {
        if (TerrainLayer == null || _tileRegistry == null)
            return;

        if (mapData.DecorationOverlays.Count == 0)
        {
            ILog.Print("[DUAL-GRID] No terrain data available, skipping dual-grid rendering");
            return;
        }

        // NO layer offset - visual grid renders at standard positions (0,0) to (size, size)
        // The data grid is conceptually at +half tile offset from visual grid
        TerrainLayer.Position = Vector2.Zero;

        // Only use transition resolver if compiled atlas is available
        // Otherwise, fallback to tile's own coordinates to avoid using sourceId=0 that doesn't exist
        if (_usingCompiledAtlas)
        {
            _transitionResolver ??= new CompiledTransitionResolver();
        }

        var tilesRendered = 0;
        var transitionsResolved = 0;
        var missingTileIds = new HashSet<string>();

        foreach (var (position, (baseTileId, topTileId, bitmask)) in mapData.DecorationOverlays)
        {
            var baseTile = _tileRegistry.GetTile(baseTileId);
            var topTile = _tileRegistry.GetTile(topTileId);

            if (baseTile == null)
            {
                missingTileIds.Add(baseTileId);
                continue;
            }

            int sourceId;
            Vector2I atlasCoords;

            // If compiled atlas is not available, always use tile's own coordinates
            if (!_usingCompiledAtlas || _transitionResolver == null)
            {
                var effectiveTile = topTile ?? baseTile;
                atlasCoords = effectiveTile.HasAutoTileVariants
                    ? effectiveTile.GetAutoTileCoords(bitmask)
                    : effectiveTile.AtlasCoords;
                sourceId = effectiveTile.SourceId;
            }
            // For uniform terrain (top == base, bitmask 15), we need the solid fill
            // Self-transitions don't exist in the map, so find ANY transition with this tile
            else if (topTileId == baseTileId && bitmask == 15)
            {
                // For compositable tiles, find the solid fill from any transition
                var solidFillCoords = _transitionResolver.ResolveSolidFill(topTileId);
                if (solidFillCoords.HasValue)
                {
                    atlasCoords = solidFillCoords.Value;
                    sourceId = _transitionResolver.CompiledAtlasSourceId;
                    transitionsResolved++;
                }
                else if (baseTile.SourceId == _transitionResolver.CompiledAtlasSourceId)
                {
                    // Tile already uses compiled atlas source - use its coordinates directly
                    atlasCoords = baseTile.HasAutoTileVariants
                        ? baseTile.GetAutoTileCoords(15)
                        : baseTile.AtlasCoords;
                    sourceId = baseTile.SourceId;
                }
                else
                {
                    // Fallback: use safe default position (atlas 0,0)
                    atlasCoords = Vector2I.Zero;
                    sourceId = _transitionResolver.CompiledAtlasSourceId;
                }
            }
            else
            {
                // Standard case: look up transition in compiled map
                var effectiveTopTile = topTile ?? baseTile;
                var fallbackCoords = effectiveTopTile.HasAutoTileVariants
                    ? effectiveTopTile.GetAutoTileCoords(bitmask)
                    : effectiveTopTile.AtlasCoords;

                var result = _transitionResolver.ResolveWithFallback(
                    topTileId, baseTileId, bitmask, effectiveTopTile.SourceId, fallbackCoords);

                atlasCoords = result.AtlasCoords;
                sourceId = result.SourceId;

                if (sourceId == _transitionResolver.CompiledAtlasSourceId)
                    transitionsResolved++;
            }

            TerrainLayer.SetCell(position, sourceId, atlasCoords);
            tilesRendered++;
        }

        var atlasStatus = _usingCompiledAtlas ? "compiled atlas" : "FALLBACK (no compiled atlas)";
        ILog.Print($"[DUAL-GRID] Rendered {tilesRendered} terrain tiles ({transitionsResolved} transitions) using {atlasStatus}");

        if (missingTileIds.Count > 0)
            ILog.Print($"[DUAL-GRID] WARNING: Missing tiles: {string.Join(", ", missingTileIds)}");
    }

    /// <summary>
    /// Render non-terrain tiles (structures, decorations, effects) from the data grid.
    /// These render at data-grid positions (no offset) on top of the dual-grid terrain.
    /// </summary>
    private void RenderNonTerrainTiles(SimpleMapData mapData)
    {
        // Ensure non-terrain layers have no offset (data grid positions, not visual grid)
        if (DecorationLayer != null) DecorationLayer.Position = Vector2.Zero;
        if (StructureLayer != null) StructureLayer.Position = Vector2.Zero;
        if (EffectLayer != null) EffectLayer.Position = Vector2.Zero;

        var structureCount = 0;
        var decorationCount = 0;
        var effectCount = 0;

        for (var y = 0; y < mapData.Size.Y; y++)
        for (var x = 0; x < mapData.Size.X; x++)
        {
            var position = new Vector2I(x, y);
            var tileId = mapData.GetTileId(position);
            var (sourceId, atlasCoords, layer, tileSize) = GetTileRenderInfoFull(tileId);

            // Skip terrain tiles - they're handled by dual-grid system
            if (layer == TileLayer.Terrain)
                continue;

            var targetLayer = layer switch
            {
                TileLayer.Decoration => DecorationLayer,
                TileLayer.Structure => StructureLayer,
                TileLayer.Effects => EffectLayer,
                _ => null
            };

            if (targetLayer == null)
                continue;

            // Render each cell of multi-tile with proper atlas offset
            for (var dy = 0; dy < tileSize.Y; dy++)
            for (var dx = 0; dx < tileSize.X; dx++)
            {
                var cellPos = new Vector2I(position.X + dx, position.Y + dy);
                if (cellPos.X >= mapData.Size.X || cellPos.Y >= mapData.Size.Y)
                    continue;

                var cellAtlasCoords = new Vector2I(atlasCoords.X + dx, atlasCoords.Y + dy);
                targetLayer.SetCell(cellPos, sourceId, cellAtlasCoords);
            }

            // Track counts for logging
            switch (layer)
            {
                case TileLayer.Structure: structureCount++; break;
                case TileLayer.Decoration: decorationCount++; break;
                case TileLayer.Effects: effectCount++; break;
            }
        }

        if (structureCount + decorationCount + effectCount > 0)
            ILog.Print($"[NON-TERRAIN] Rendered {structureCount} structures, {decorationCount} decorations, {effectCount} effects");
    }

    /// <summary>
    /// Render a tile to the appropriate TileMapLayer based on its layer property.
    /// </summary>
    private void RenderTileToLayer(Vector2I position, string tileId)
    {
        var (sourceId, atlasCoords, layer) = GetTileRenderInfoWithLayer(tileId);

        var targetLayer = layer switch
        {
            TileLayer.Terrain => TerrainLayer,
            TileLayer.Decoration => DecorationLayer,
            TileLayer.Structure => StructureLayer,
            TileLayer.Effects => EffectLayer,
            _ => TerrainLayer
        };

        targetLayer?.SetCell(position, sourceId, atlasCoords);
    }

    /// <summary>
    /// Get full render info for a tile including size for multi-tile support.
    /// Returns (sourceId, atlasCoords, layer, size) from TileRegistry.
    /// </summary>
    private (int sourceId, Vector2I atlasCoords, TileLayer layer, Vector2I size) GetTileRenderInfoFull(string tileId)
    {
        if (_tileRegistry == null)
        {
            ILog.Print($"[TILE DEBUG] Registry is NULL for tileId='{tileId}', using fallback");
            var fallbackCoords = tileId == SimpleMapGenerator.WallTileId
                ? new Vector2I(2, 0)
                : new Vector2I(4, 0);
            return (4, fallbackCoords, TileLayer.Terrain, Vector2I.One);
        }

        var tile = _tileRegistry.GetTile(tileId);
        if (tile == null)
        {
            ILog.Print($"[TILE DEBUG] Tile not found in registry: '{tileId}', using fallback");
            return (4, new Vector2I(4, 0), TileLayer.Terrain, Vector2I.One);
        }

        return (tile.SourceId, tile.AtlasCoords, tile.Layer, tile.Size);
    }

    /// <summary>
    /// Get the source ID, atlas coordinates, and layer for rendering a tile.
    /// Returns (sourceId, atlasCoords, layer) from TileRegistry.
    /// </summary>
    private (int sourceId, Vector2I atlasCoords, TileLayer layer) GetTileRenderInfoWithLayer(string tileId)
    {
        var (sourceId, atlasCoords, layer, _) = GetTileRenderInfoFull(tileId);
        return (sourceId, atlasCoords, layer);
    }

    /// <summary>
    /// Get the source ID and atlas coordinates for rendering a tile.
    /// Used for overlay rendering which doesn't need layer information.
    /// </summary>
    private (int sourceId, Vector2I atlasCoords) GetTileRenderInfo(string tileId)
    {
        var (sourceId, atlasCoords, _) = GetTileRenderInfoWithLayer(tileId);
        return (sourceId, atlasCoords);
    }

    private void LogTileRenderingSample()
    {
        if (_hasLoggedTileInfo || _mapData == null) return;
        _hasLoggedTileInfo = true;

        ILog.Print($"[TILE DEBUG] === Tile Rendering Debug ===");
        ILog.Print($"[TILE DEBUG] TileRegistry available: {_tileRegistry != null}");
        ILog.Print($"[TILE DEBUG] TerrainLayer available: {TerrainLayer != null}");

        if (_tileRegistry != null)
        {
            foreach (var tile in _tileRegistry.GetAllTiles())
            {
                ILog.Print(
                    $"[TILE DEBUG] Registered: id='{tile.Id}' sourceId={tile.SourceId} atlas={tile.AtlasCoords} passable={tile.IsPassable}");
            }
        }

        // Log sample of actual map tiles
        var sampleCount = 0;
        for (var y = 0; y < _mapData.Size.Y && sampleCount < 10; y++)
        for (var x = 0; x < _mapData.Size.X && sampleCount < 10; x++)
        {
            var pos = new Vector2I(x, y);
            var tileId = _mapData.GetTileId(pos);
            var (sourceId, atlasCoords) = GetTileRenderInfo(tileId);
            ILog.Print($"[TILE DEBUG] Map[{x},{y}] = '{tileId}' -> sourceId={sourceId} atlas={atlasCoords}");
            sampleCount++;
        }
    }

    private void SetupViewport(Vector2I mapSize)
    {
        if (Viewport == null) return;

        // Size viewport to match map dimensions
        Viewport.Size = new Vector2I((mapSize.X + 1) * TILE_SIZE, (mapSize.Y + 1) * TILE_SIZE);
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;

        // Get camera reference
        _camera2D = Viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D != null) _camera2D.Enabled = true;
    }

    private void ConfigureCamera()
    {
        if (_camera2D == null || TerrainLayer == null || Viewport == null) return;

        var usedRect = TerrainLayer.GetUsedRect();
        if (usedRect.Size == Vector2I.Zero) return;

        // Calculate the center of the map area in pixel coordinates
        var mapCenter = new Vector2(
            (usedRect.Position.X * TILE_SIZE) + (usedRect.Size.X * TILE_SIZE / 2f),
            (usedRect.Position.Y * TILE_SIZE) + (usedRect.Size.Y * TILE_SIZE / 2f)
        );
        _camera2D.GlobalPosition = mapCenter;

        // Calculate zoom to fit map in viewport
        var mapPixelSize = new Vector2(usedRect.Size.X * TILE_SIZE, usedRect.Size.Y * TILE_SIZE);
        var viewportSize = Viewport.Size;

        var zoomX = viewportSize.X / mapPixelSize.X;
        var zoomY = viewportSize.Y / mapPixelSize.Y;
        var zoom = Mathf.Min(zoomX, zoomY);

        _camera2D.Zoom = new Vector2(zoom, zoom);

        ILog.Print($"Camera configured: center={mapCenter}, zoom={zoom}");
    }

    private void CreateEnemySprites(List<Vector2I> enemyPositions)
    {
        foreach (var pos in enemyPositions)
        {
            var enemySprite = new Sprite2D();
            enemySprite.Texture = CreateColorTexture(Colors.Red, 16);
            enemySprite.Position = new Vector2(pos.X * TILE_SIZE + TILE_SIZE / 2, pos.Y * TILE_SIZE + TILE_SIZE / 2);
            enemySprite.ZIndex = 200; // Above fog of war

            if (Viewport != null) Viewport.AddChild(enemySprite);
            _enemySprites.Add(enemySprite);
        }
    }

    private void UpdatePlayerSpritePosition(Vector2I gridPosition)
    {
        if (PlayerSprite != null)
            PlayerSprite.Position = new Vector2(
                gridPosition.X * TILE_SIZE + TILE_SIZE / 2,
                gridPosition.Y * TILE_SIZE + TILE_SIZE / 2
            );
    }

    private ImageTexture CreateColorTexture(Color color, int size)
    {
        var image = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        image.Fill(color);
        return ImageTexture.CreateFromImage(image);
    }

    private void SetupScreenMesh()
    {
        if (ScreenMesh == null) return;

        // Create a custom mesh with explicit UV coordinates
        var arrayMesh = new ArrayMesh();
        var arrays = new Array();
        arrays.Resize((int)Mesh.ArrayType.Max);

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
        arrays[(int)Mesh.ArrayType.Vertex] = vertices;
        arrays[(int)Mesh.ArrayType.TexUV] = uvs;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        // Create the mesh surface
        arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        // Assign the custom mesh
        ScreenMesh.Mesh = arrayMesh;

        ILog.Print("Custom screen mesh with proper UVs created");
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

        ILog.Print("Screen material setup complete");
    }
}
