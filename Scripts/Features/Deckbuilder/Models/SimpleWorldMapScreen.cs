using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Enhanced world map screen with visual feedback for exploration and combat
/// </summary>
[Tool]
[GlobalClass]
public partial class SimpleWorldMapScreen : Node3D
{
    // Export properties for editor assignment
    [Export] public TileMapLayer? MapLayer { get; set; }
    [Export] public TileMapLayer? OverlayLayer { get; set; }
    [Export] public SubViewport? Viewport { get; set; }
    [Export] public MeshInstance3D? ScreenMesh { get; set; }
    [Export] public Label? StatusLabel { get; set; }
    [Export] public Sprite2D? PlayerSprite { get; set; }
    [Export] public Control? CombatUI { get; set; }

    // Combat UI elements
    private ProgressBar? _playerHealthBar;
    private ProgressBar? _enemyHealthBar;
    private Label? _playerHealthLabel;
    private Label? _enemyHealthLabel;
    private Label? _actionLabel;

    private SimpleMapData? _mapData;
    private List<Sprite2D> _enemySprites = new();
    private IGameSessionService? _gameSession;
    private bool _isInitialized = false;
    private bool _serviceReady = false;
    private readonly HashSet<Vector2I> _renderedVisitedTiles = new();

    // Pending initialization data (stored if Initialize called before service ready)
    private CardSignature[]? _pendingMapSeed;
    private CardSignature[]? _pendingAbilities;

    // Visual constants
    private const int TILE_SIZE = 16;
    private Camera2D? _camera2D;
    private ITileRegistry? _tileRegistry;

    public override void _Ready()
    {
        SetupCombatUIReferences();

        if (StatusLabel != null) StatusLabel.Text = "Waiting for map data...";

        if (CombatUI != null) CombatUI.Visible = false;

        CallDeferred(nameof(SetupScreenMesh));
        CallDeferred(nameof(SetupScreenMaterial));

        // Get tile registry for rendering
        ServiceLocator.Get<ITileRegistry>(registry => _tileRegistry = registry);

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
            gameSession.VisitedTilesUpdated += OnVisitedTilesUpdated;

            // If Initialize() was called before service was ready, start now
            if (_pendingMapSeed != null && _pendingAbilities != null)
            {
                ILog.Print("Processing pending initialization...");
                gameSession.StartSession(_pendingMapSeed[0], _pendingAbilities.ToList());
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
            _gameSession.VisitedTilesUpdated -= OnVisitedTilesUpdated;
        }
    }

    public void Initialize(CardSignature[] mapSeed, CardSignature[] abilities)
    {
        ILog.Print(
            $"Initializing simple map screen with {mapSeed.Length} seed card(s) and {abilities.Length} abilities");
        ILog.Print($"🐛 CardSignature Elements: [{string.Join(", ", mapSeed[0].Elements.Select(e => e.ToString("F3")))}]");

        if (_serviceReady && _gameSession != null)
        {
            // Service is ready, start immediately
            _gameSession.StartSession(mapSeed[0], abilities.ToList());
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
        // Map will be received via OnMapGenerated event
    }

    private void OnMapGenerated(SimpleMapData mapData)
    {
        ILog.Print($"Received map from GameSessionService: {mapData.Size.X}x{mapData.Size.Y}");
        _mapData = mapData;
        _renderedVisitedTiles.Clear();
        RenderMap(_mapData);
    }

    private void HandleExplorationStart()
    {
        if (StatusLabel != null) StatusLabel.Text = "Exploring map...";

        // Just show the player sprite - GameSessionService handles the exploration logic
        // and sends us PlayerMoved events
        if (PlayerSprite != null)
        {
            PlayerSprite.Visible = true;
        }
    }

    private void OnServicePlayerMoved(Vector2I newPosition)
    {
        UpdatePlayerSpritePosition(newPosition);
    }

    private void OnVisitedTilesUpdated(IReadOnlySet<Vector2I> visitedTiles)
    {
        if (OverlayLayer == null || _mapData == null) return;

        // Get the visited tile render info once
        var (visitedSourceId, visitedAtlasCoords) = GetTileRenderInfo("floor_visited");

        foreach (var position in visitedTiles)
        {
            // Skip if already rendered as visited
            if (_renderedVisitedTiles.Contains(position)) continue;

            // Only mark passable, non-enemy tiles
            if (_mapData.IsPassable(position) && !_mapData.EnemyPositions.Contains(position))
            {
                OverlayLayer.SetCell(position, visitedSourceId, visitedAtlasCoords);
                _renderedVisitedTiles.Add(position);
            }
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

        // Loot will be spawned when the LootGenerated event fires
        // No need to manually trigger it here
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

    private void RenderMap(SimpleMapData mapData)
    {
        if (MapLayer == null)
        {
            ILog.Error("RenderMap: MapLayer is null!");
            return;
        }

        MapLayer.Clear();
        OverlayLayer?.Clear();

        // Clear old enemy sprites
        foreach (var sprite in _enemySprites) sprite?.QueueFree();
        _enemySprites.Clear();

        // Resize viewport to fit the map
        SetupViewport(mapData.Size);

        // Log tile info for debugging
        LogTileRenderingSample();

        // Render the map using tile registry for atlas coordinates and source IDs
        for (var y = 0; y < mapData.Size.Y; y++)
        for (var x = 0; x < mapData.Size.X; x++)
        {
            var position = new Vector2I(x, y);
            var tileId = mapData.GetTileId(position);
            var (sourceId, atlasCoords) = GetTileRenderInfo(tileId);
            MapLayer.SetCell(position, sourceId, atlasCoords);
        }

        // Create enemy sprites
        CreateEnemySprites(mapData.EnemyPositions);

        // Configure camera after map is rendered (deferred to ensure GetUsedRect works)
        CallDeferred(nameof(ConfigureCamera));

        ILog.Print($"Rendered map: {mapData.Size.X}x{mapData.Size.Y} with {mapData.EnemyPositions.Count} enemies");
    }

    /// <summary>
    /// Get the source ID and atlas coordinates for rendering a tile.
    /// Returns (sourceId, atlasCoords) from TileRegistry.
    /// </summary>
    private (int sourceId, Vector2I atlasCoords) GetTileRenderInfo(string tileId)
    {
        if (_tileRegistry == null)
        {
            ILog.Print($"[TILE DEBUG] Registry is NULL for tileId='{tileId}', using fallback");
            var fallbackCoords = tileId == SimpleMapGenerator.WallTileId
                ? new Vector2I(2, 0)
                : new Vector2I(4, 0);
            return (4, fallbackCoords); // Use source 4 as fallback
        }

        var tile = _tileRegistry.GetTile(tileId);
        if (tile == null)
        {
            ILog.Print($"[TILE DEBUG] Tile not found in registry: '{tileId}', using fallback");
            return (4, new Vector2I(4, 0)); // Default fallback with source 4
        }

        return (tile.SourceId, tile.AtlasCoords);
    }

    private bool _hasLoggedTileInfo = false;
    private void LogTileRenderingSample()
    {
        if (_hasLoggedTileInfo || _mapData == null) return;
        _hasLoggedTileInfo = true;

        ILog.Print($"[TILE DEBUG] === Tile Rendering Debug ===");
        ILog.Print($"[TILE DEBUG] TileRegistry available: {_tileRegistry != null}");
        ILog.Print($"[TILE DEBUG] MapLayer available: {MapLayer != null}");

        if (_tileRegistry != null)
        {
            foreach (var tile in _tileRegistry.GetAllTiles())
            {
                ILog.Print($"[TILE DEBUG] Registered: id='{tile.Id}' sourceId={tile.SourceId} atlas={tile.AtlasCoords} passable={tile.IsPassable}");
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
        Viewport.Size = new Vector2I(mapSize.X * TILE_SIZE, mapSize.Y * TILE_SIZE);
        Viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.WhenParentVisible;

        // Get camera reference
        _camera2D = Viewport.GetNodeOrNull<Camera2D>("Camera2D");
        if (_camera2D != null) _camera2D.Enabled = true;
    }

    private void ConfigureCamera()
    {
        if (_camera2D == null || MapLayer == null || Viewport == null) return;

        var usedRect = MapLayer.GetUsedRect();
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
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
    
        // Define the quad vertices (matching the desired screen size)
        var vertices = new Vector3[]
        {
            new(-2.6665f, -1.5f, 0), // Bottom-left
            new(2.6665f, -1.5f, 0),  // Bottom-right  
            new(2.6665f, 1.5f, 0),   // Top-right
            new(-2.6665f, 1.5f, 0)   // Top-left
        };
    
        // Critical: UV coordinates that properly map the texture
        var uvs = new Vector2[]
        {
            new(0, 1), // Bottom-left maps to (0,1) - bottom of texture
            new(1, 1), // Bottom-right maps to (1,1) - bottom-right of texture
            new(1, 0), // Top-right maps to (1,0) - top-right of texture  
            new(0, 0)  // Top-left maps to (0,0) - top-left of texture
        };
    
        // Triangle indices for two triangles making a quad
        var indices = new int[]
        {
            0, 1, 2,  // First triangle
            0, 2, 3   // Second triangle
        };
    
        // Normals pointing toward camera
        var normals = new Vector3[]
        {
            Vector3.Forward, Vector3.Forward, Vector3.Forward, Vector3.Forward
        };
    
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