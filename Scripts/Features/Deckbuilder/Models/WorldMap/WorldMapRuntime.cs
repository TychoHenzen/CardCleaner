using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Components;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Tiles;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// Runtime of the simple world map screen: connects the game session and tile registry to the screen's
/// rendering, fog, combat and preview components. The screen node keeps the Godot-facing surface.
/// </summary>
internal sealed class WorldMapRuntime : ISessionView
{
    private const float LootCardSpacing = 0.5f;

    private readonly SimpleWorldMapScreen _screen;
    private readonly WorldMapComponents _components;
    private readonly BiomePreviewHost _biomePreview = new();
    private readonly PathDebugOverlay _pathOverlay = new();
    private readonly WorldMapCameraRig _cameraRig;
    private readonly EnemySpriteLayer _enemySprites;
    private readonly WorldMapCombatUI _combatUI;

    // The grid of the shown map: converts the session's cell ids and world positions to tiles.
    private IGridMapData? _grid;

    // The screen acts only on a session it started: the link passes events on while the screen owns the session.
    private readonly SessionViewLink _link;
    private readonly IServiceResolver _services;

    internal WorldMapRuntime(SimpleWorldMapScreen screen, IServiceResolver services)
    {
        _screen = screen;
        _services = services;
        _components = new WorldMapComponents(screen);
        _cameraRig = new WorldMapCameraRig(screen.Viewport);
        _enemySprites = new EnemySpriteLayer(screen.Viewport);
        _combatUI = new WorldMapCombatUI(screen.CombatUI);
        _link = new SessionViewLink(this);
    }

    internal void Start()
    {
        _combatUI.Hide();
        _biomePreview.Setup(_screen.Viewport);
        _cameraRig.ConfigureForPreview();
        ShowStatus("Waiting for map data...");

        _services.Get<ITileRegistry>(_components.Initialize);
        _services.Get<IGameSessionService>(_link.Connect);
    }

    internal void Stop()
    {
        _link.Disconnect();
    }

    // A session service that resolves after the screen left the tree or was freed must not subscribe it.
    bool ISessionView.CanConnect => GodotObject.IsInstanceValid(_screen) && _screen.IsInsideTree();

    internal void StartSession(CardSignature[] mapSeed, CardSignature[] abilities, IMapGenerator generator)
    {
        _link.Start(mapSeed, abilities, generator);
    }

    internal void UpdateBiomePreview(CardSignature[] cards) => _biomePreview.Update(cards);

    internal void Reset()
    {
        _link.Reset();

        _grid = null;

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

    void ISessionView.OnStateChanged(SessionState newState)
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

        var combat = new CombatVisualization(
            _screen,
            _combatUI,
            playerWon => _screen.CallDeferred(SimpleWorldMapScreen.MethodName.EndCombat, playerWon));
        combat.Start();
    }

    /// <summary>
    /// Shows the map of a session this screen started. The session's generator must give a grid map: the screen
    /// checks that here, once, and any other map throws, which ends the session.
    /// </summary>
    void ISessionView.OnMapReady(IGeneratedMap generated)
    {
        if (generated is not IGridGeneratedMap gridMap)
            throw new InvalidOperationException("The session's generator returned no grid map");

        var mapData = gridMap.RawMapData;
        _grid = gridMap.GridMapData;
        ILog.Print($"Received map from GameSessionService: {mapData.Size.X}x{mapData.Size.Y}");
        _pathOverlay.ForgetRenderedTiles();
        RenderMap(mapData);
        _components.Fog?.Initialize(mapData.Size);
        ILog.Print($"[FOG] Initialized fog of war with {_components.Fog?.SpriteCount ?? 0} fog sprites");
    }

    private void RenderMap(IGridMap mapData)
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

    void ISessionView.OnPlayerMoved(Vector2 worldPosition)
    {
        if (_screen.PlayerSprite != null && _grid != null)
        {
            var tile = _grid.WorldToGrid(worldPosition);
            _screen.PlayerSprite.Position = new Vector2(
                tile.X * _components.TileSize + _components.TileSize / 2,
                tile.Y * _components.TileSize + _components.TileSize / 2
            );
        }

        // The grid view shows a move at once, so it reports it shown straight away.
        _link.ReportStepShown();
    }

    void ISessionView.OnPathUpdated(IReadOnlyList<int> pathCells, int? targetCell)
    {
        if (_grid is not { } grid) return;

        if (_screen.OverlayLayer == null || _components.Terrain == null) return;

        var path = pathCells.Select(grid.CellIdToPosition).ToList();
        Vector2I? target = targetCell.HasValue ? grid.CellIdToPosition(targetCell.Value) : null;
        _pathOverlay.Show(_screen.OverlayLayer, _components.Terrain, path, target);
    }

    // The fog numbers its tiles row by row like the grid's cell ids, so it takes the session's cells as they are.
    void ISessionView.OnVisibilityUpdated(IReadOnlySet<int> seenCells, IReadOnlySet<int> visibleCells)
    {
        _components.Fog?.UpdateVisibility(seenCells, visibleCells);
    }

    void ISessionView.OnEnemyDefeated(int cellId)
    {
        if (_grid == null) return;

        var tile = _grid.CellIdToPosition(cellId);
        ILog.Print($"Enemy defeated at {tile} - removing sprite");
        _enemySprites.RemoveAt(tile, _components.TileSize);
    }

    void ISessionView.OnLootGenerated(List<CardSignature> lootSignatures)
    {
        ILog.Print($"Loot generated: {lootSignatures.Count} cards");
        if (ILog.ExportCheck(_screen.LootSpawnPoint, nameof(SimpleWorldMapScreen.LootSpawnPoint), _screen))
            return;

        SpawnLootRow(lootSignatures, _screen.LootSpawnPoint!.GlobalTransform);
    }

    // Lays the loot cards out in a row that starts at the loot point and runs along that point's X axis.
    private void SpawnLootRow(List<CardSignature> lootSignatures, Transform3D lootPoint)
    {
        var rowDirection = lootPoint.Basis.Orthonormalized().X;

        _services.Get<ICardSpawningService>(spawningService =>
            _services.Get<ICardSpawner>(spawner =>
            {
                for (var i = 0; i < lootSignatures.Count; i++)
                {
                    var spawnTransform = Transform3D.Identity;
                    spawnTransform.Origin = lootPoint.Origin + rowDirection * (i * LootCardSpacing);
                    spawningService.SpawnCard(lootSignatures[i], spawnTransform, spawner.GetNode());
                }

                ILog.Print($"Spawned {lootSignatures.Count} loot cards!");
            }));
    }

    // The grid view hands over the registered checker (or none) and clears its map when the new one arrives.
    // ASSUMPTION: the registered checker is the same stateless SimpleVisibilityChecker exploration falls back to, so
    // handing it over leaves exploration unchanged.
    IVisibilityChecker? ISessionView.PrepareForSession() =>
        _services.Has<IVisibilityChecker>() ? _services.Get<IVisibilityChecker>() : null;

    // The grid view keeps nothing per session that Reset does not clear.
    void ISessionView.OnSessionReleased()
    {
    }

    private void ShowStatus(string text)
    {
        if (_screen.StatusLabel != null) _screen.StatusLabel.Text = text;
    }
}
