using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Runtime of the simple world map screen: connects the game session and tile registry to the screen's
/// rendering, fog, combat and preview components. The screen node keeps the Godot-facing surface.
/// </summary>
internal sealed class WorldMapRuntime
{
    private readonly SimpleWorldMapScreen _screen;
    private readonly WorldMapComponents _components;
    private readonly BiomePreviewHost _biomePreview = new();
    private readonly PathDebugOverlay _pathOverlay = new();
    private readonly WorldMapCameraRig _cameraRig;
    private readonly EnemySpriteLayer _enemySprites;
    private readonly WorldMapCombatUI _combatUI;

    private IGameSessionService? _gameSession;
    private CardSignature[]? _pendingAbilities;
    private CardSignature[]? _pendingMapSeed;
    private bool _serviceReady;


    internal WorldMapRuntime(SimpleWorldMapScreen screen)
    {
        _screen = screen;
        _components = new WorldMapComponents(screen);
        _cameraRig = new WorldMapCameraRig(screen.Viewport);
        _enemySprites = new EnemySpriteLayer(screen.Viewport);
        _combatUI = new WorldMapCombatUI(screen.CombatUI);
    }

    internal void Start()
    {
        _combatUI.Hide();
        _biomePreview.Setup(_screen.Viewport);
        _cameraRig.ConfigureForPreview();
        ShowStatus("Waiting for map data...");

        ServiceLocator.Get<ITileRegistry>(_components.Initialize);
        ServiceLocator.Get<IGameSessionService>(OnGameSessionReady);
    }

    internal void Stop()
    {
        if (_gameSession == null) return;

        _gameSession.StateChanged -= OnSessionStateChanged;
        _gameSession.MapGenerated -= OnMapGenerated;
        _gameSession.LootGenerated -= OnLootGenerated;
        _gameSession.PlayerMoved -= OnServicePlayerMoved;
        _gameSession.EnemyDefeated -= OnServiceEnemyDefeated;
        _gameSession.VisibilityUpdated -= OnVisibilityUpdated;
        _gameSession.PathUpdated -= OnPathUpdated;
    }

    internal void StartSession(CardSignature[] mapSeed, CardSignature[] abilities)
    {
        if (_serviceReady && _gameSession != null)
        {
            _gameSession.StartSession(mapSeed.ToList(), abilities.ToList());
            return;
        }

        ILog.Print("GameSessionService not ready, storing pending initialization...");
        _pendingMapSeed = mapSeed;
        _pendingAbilities = abilities;
    }

    internal void UpdateBiomePreview(CardSignature[] cards) => _biomePreview.Update(cards);

    internal void Reset()
    {
        // Clear all layers
        _components.Terrain?.ClearAllLayers();
        _screen.OverlayLayer?.Clear();

        // Clear managed components
        _components.Fog?.Clear();
        _components.BiomeOverlay?.Clear();
        _enemySprites.Clear();
        _pathOverlay.ForgetRenderedTiles();

        // Hide player sprite
        if (_screen.PlayerSprite != null)
            _screen.PlayerSprite.Visible = false;

        _combatUI.Hide();
        ShowStatus("Waiting for map data...");

        // Show and clear biome preview
        _biomePreview.ShowCleared();
        _cameraRig.ConfigureForPreview();
    }

    internal void ConfigureCamera() => _cameraRig.FitToMap(_screen.TerrainLayer, _components.TileSize);

    internal void EndCombat(bool playerWon)
    {
        _combatUI.Hide();
        ShowStatus(playerWon ? "Victory! Generating loot..." : "Defeat!");
    }

    // Use dependency injection to get the game session service
    private void OnGameSessionReady(IGameSessionService gameSession)
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
    }

    private void OnSessionStateChanged(SessionState newState)
    {
        ShowStatus($"Status: {newState}");

        switch (newState)
        {
            case SessionState.GeneratingMap:
                ShowStatus("Generating map...");
                _biomePreview.Hide();
                break;
            case SessionState.Exploring:
                HandleExplorationStart();
                break;
            case SessionState.InCombat:
                HandleCombatStart();
                break;
            case SessionState.GeneratingLoot:
                ShowStatus("Generating loot...");
                _components.Fog?.RevealAll();
                ILog.Print("[FOG] Revealed entire map");
                break;
            case SessionState.SessionComplete:
                ShowStatus("Session complete!");
                ILog.Print("Session complete - ready for next round!");
                break;
        }
    }

    private void HandleExplorationStart()
    {
        ShowStatus("Exploring map...");

        if (_screen.PlayerSprite != null)
        {
            _screen.PlayerSprite.Visible = true;
            _screen.PlayerSprite.ZIndex = 200;
        }
    }

    private void HandleCombatStart()
    {
        ShowStatus("Combat started!");
        _combatUI.Show();

        if (_gameSession == null) return;

        var combat = new CombatVisualization(
            _screen,
            _combatUI,
            playerWon => _screen.CallDeferred(SimpleWorldMapScreen.MethodName.EndCombat, playerWon));
        combat.Start();
    }

    private void OnMapGenerated(SimpleMapData mapData)
    {
        ILog.Print($"Received map from GameSessionService: {mapData.Size.X}x{mapData.Size.Y}");
        _pathOverlay.ForgetRenderedTiles();
        RenderMap(mapData);
        _components.Fog?.Initialize(mapData.Size);
        ILog.Print($"[FOG] Initialized fog of war with {_components.Fog?.SpriteCount ?? 0} fog sprites");
    }

    private void RenderMap(SimpleMapData mapData)
    {
        if (_screen.TerrainLayer == null || _components.Terrain == null)
        {
            ILog.Error("RenderMap: TerrainLayer or TerrainRenderer is null!");
            return;
        }

        // Clear all layers and old sprites
        _components.Terrain.ClearAllLayers();
        _screen.OverlayLayer?.Clear();
        _enemySprites.Clear();

        // Resize viewport to fit the map
        _cameraRig.ResizeViewport(mapData.Size, _components.TileSize);

        // Log tile info for debugging
        _components.Terrain.LogTileRenderingSample(mapData);

        // Render terrain and non-terrain tiles
        _components.Terrain.RenderTerrainTransitions(mapData);
        _components.Terrain.RenderNonTerrainTiles(mapData);

        // Render biome overlay if enabled
        if (_screen.ShowBiomeOverlay)
            _components.BiomeOverlay?.Render(mapData);

        // Create enemy sprites
        _enemySprites.Create(mapData.EnemyPositions, _components.TileSize);

        // Configure camera after map is rendered
        _screen.CallDeferred(SimpleWorldMapScreen.MethodName.ConfigureCamera);

        ILog.Print($"Rendered map: {mapData.Size.X}x{mapData.Size.Y} with {mapData.EnemyPositions.Count} enemies");
    }

    private void OnServicePlayerMoved(Vector2I gridPosition)
    {
        if (_screen.PlayerSprite != null)
            _screen.PlayerSprite.Position = new Vector2(
                gridPosition.X * _components.TileSize + _components.TileSize / 2,
                gridPosition.Y * _components.TileSize + _components.TileSize / 2
            );
    }

    private void OnPathUpdated(IReadOnlyList<Vector2I> path, Vector2I? target)
    {
        if (_screen.OverlayLayer == null || _components.Terrain == null) return;

        _pathOverlay.Show(_screen.OverlayLayer, _components.Terrain, path, target);
    }

    private void OnVisibilityUpdated(IReadOnlySet<Vector2I> seenTiles, IReadOnlySet<Vector2I> currentlyVisibleTiles)
    {
        _components.Fog?.UpdateVisibility(seenTiles, currentlyVisibleTiles);
    }

    private void OnServiceEnemyDefeated(Vector2I position)
    {
        ILog.Print($"Enemy defeated at {position} - removing sprite");
        _enemySprites.RemoveAt(position, _components.TileSize);
    }

    private void OnLootGenerated(List<CardSignature> lootSignatures)
    {
        ILog.Print($"Loot generated: {lootSignatures.Count} cards");
        LootCardSpawner.Spawn(lootSignatures);
    }

    private void ShowStatus(string text)
    {
        if (_screen.StatusLabel != null) _screen.StatusLabel.Text = text;
    }
}
