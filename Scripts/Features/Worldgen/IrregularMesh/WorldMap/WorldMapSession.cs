using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;

/// <summary>
/// The runtime built for one generated map: map data, enemies, combat, fog of war, rendering and exploration.
/// </summary>
internal sealed class WorldMapSession
{
    private readonly WorldMapSettings _settings;
    private readonly WorldMapRenderLayer _renderLayer;

    private IrregularMapCombatHandler? _combatHandler;
    private IrregularMapEnemyManager? _enemyManager;
    private IVisibilityChecker? _visibilityChecker;

    internal WorldMapSession(WorldMapSettings settings, IrregularMesh mesh)
    {
        _settings = settings;
        MapData = new IrregularMeshMapData(mesh) { WorldScale = settings.WorldScale };
        _renderLayer = new WorldMapRenderLayer(settings, mesh, MapData);
    }

    internal IrregularMeshMapData MapData { get; }

    internal IrregularMeshFogOfWar? FogOfWar { get; private set; }

    internal IrregularMeshExplorationController? ExplorationController { get; private set; }

    internal event Action<int, Vector2>? PlayerMoved;

    internal event Action<Vector2>? PositionUpdated;

    internal event Action<IReadOnlySet<int>>? VisibilityChanged;

    /// <summary>
    /// Raised when the session is over: the map is fully explored or the player lost a combat.
    /// </summary>
    internal event Action? ExplorationFinished;

    internal event Action<int>? EnemySpotted;

    internal event Action<int>? EnemyEncountered;

    internal event Action<int>? EnemyDefeated;

    /// <summary>
    /// Creates enemies, fog of war and renderers. Returns the player start cell.
    /// </summary>
    internal int Prepare(int seed, List<CardSignature>? abilityCards)
    {
        InitializeComponents(abilityCards);

        var playerStartCell = FindPlayerStartCell();
        _enemyManager?.PlaceEnemies(seed, playerStartCell);
        _enemyManager?.CreateEnemySprites();

        _visibilityChecker = new SimpleVisibilityChecker();

        InitializeFogOfWar();
        _renderLayer.Build(FogOfWar);
        return playerStartCell;
    }

    /// <summary>
    /// Upgrades visibility to raycasting when enabled and wires up exploration.
    /// </summary>
    internal void Complete(int playerStartCell)
    {
        UpgradeToRaycastVisibility();
        SetupExploration(playerStartCell);
    }

    internal void StartExploration() => ExplorationController?.StartExploration();

    internal void StopExploration() => ExplorationController?.StopExploration();

    internal void RevealAll() => FogOfWar?.RevealAll();

    /// <summary>
    /// Resets fog of war, frees the renderers and clears enemy sprites.
    /// </summary>
    internal void Reset()
    {
        FogOfWar?.Reset();
        _renderLayer.Clear();
        _enemyManager?.ClearSprites();
    }

    /// <summary>
    /// Ends the session: stops exploration, detaches from events and frees its controller, renderers and sprites.
    /// </summary>
    internal void Close()
    {
        StopExploration();
        Detach();
        ExplorationController?.QueueFree();
        ExplorationController = null;
        Reset();
    }

    /// <summary>
    /// Unsubscribes from fog of war, exploration and combat events.
    /// </summary>
    internal void Detach()
    {
        if (_combatHandler != null)
            _combatHandler.CombatEnded -= OnCombatEnded;

        if (FogOfWar != null)
            FogOfWar.VisibilityChanged -= OnVisibilityChanged;

        if (ExplorationController != null)
        {
            ExplorationController.PlayerMoved -= OnPlayerMoved;
            ExplorationController.PositionUpdated -= OnPositionUpdated;
            ExplorationController.ExplorationFinished -= OnExplorationFinished;
            ExplorationController.EnemyEncountered -= OnEnemyEncountered;
            ExplorationController.EnemySpotted -= OnEnemySpotted;
        }
    }

    private void InitializeComponents(List<CardSignature>? abilityCards)
    {
        if (_settings.Viewport == null) return;

        _enemyManager = new IrregularMapEnemyManager(MapData, _settings.Viewport);
        _combatHandler = new IrregularMapCombatHandler(MapData, _settings.Rng, abilityCards);
        _combatHandler.CombatEnded += OnCombatEnded;
    }

    private int FindPlayerStartCell()
    {
        for (int i = 0; i < MapData.CellCount; i++)
        {
            if (MapData.IsPassable(i))
                return i;
        }
        return 0;
    }

    private void InitializeFogOfWar()
    {
        if (!_settings.FogOfWarEnabled || _visibilityChecker == null) return;

        FogOfWar = new IrregularMeshFogOfWar(MapData, _visibilityChecker)
        {
            VisionRange = _settings.VisionRange,
            CellsPerUnit = _settings.WorldScale
        };
        FogOfWar.VisibilityChanged += OnVisibilityChanged;
        _enemyManager?.BindFog(FogOfWar);
    }

    private void UpgradeToRaycastVisibility()
    {
        var viewport = _settings.Viewport;
        if (!_settings.UseRaycastVisibility || viewport == null) return;

        _visibilityChecker = new RaycastVisibilityChecker(viewport.World2D.DirectSpaceState);
        FogOfWar?.SetVisibilityChecker(_visibilityChecker);
    }

    private void SetupExploration(int startCell)
    {
        var viewport = _settings.Viewport;
        if (viewport == null) return;

        var controller = new IrregularMeshExplorationController
        {
            Name = "ExplorationController",
            StepDelay = _settings.MovementSpeed
        };
        viewport.AddChild(controller);
        ExplorationController = controller;

        controller.Initialize(MapData, startCell, _visibilityChecker, FogOfWar);

        controller.PlayerMoved += OnPlayerMoved;
        controller.PositionUpdated += OnPositionUpdated;
        controller.ExplorationFinished += OnExplorationFinished;
        controller.EnemyEncountered += OnEnemyEncountered;
        controller.EnemySpotted += OnEnemySpotted;
    }

    private void OnPlayerMoved(int cellId, Vector2 worldPos) => PlayerMoved?.Invoke(cellId, worldPos);

    private void OnPositionUpdated(Vector2 worldPos) => PositionUpdated?.Invoke(worldPos);

    private void OnVisibilityChanged(IReadOnlySet<int> changedCells) => VisibilityChanged?.Invoke(changedCells);

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
        _combatHandler?.StartCombat(cellId, _settings.CombatHost);
    }

    private void OnCombatEnded(int cellId, bool playerWon)
    {
        if (!playerWon)
        {
            GD.Print("[IrregularWorldMapScreen] Player defeated!");
            ExplorationFinished?.Invoke();
            return;
        }

        MapData.RemoveEnemySpawn(cellId);
        _enemyManager?.RemoveEnemyAt(cellId);
        EnemyDefeated?.Invoke(cellId);

        if (MapData.EnemySpawnCells.Count > 0)
        {
            GD.Print("[IrregularWorldMapScreen] Resuming exploration...");
            ExplorationController?.ResetCombatState();
            ExplorationController?.StartExploration();
            return;
        }

        GD.Print("[IrregularWorldMapScreen] All enemies defeated! Revealing map...");
        RevealAll();
        ExplorationFinished?.Invoke();
    }
}
