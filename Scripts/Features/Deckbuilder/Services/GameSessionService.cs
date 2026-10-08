using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using Godot;
using Timer = Godot.Timer;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

[Service(typeof(IGameSessionService))]
public partial class GameSessionService : Node, IGameSessionService
{
    private readonly SessionServices _services = new();
    private readonly SessionWorld _world = new();
    private readonly GridCellEventAdapter _eventAdapter = new();
    private readonly GenerationTracker _generationTracker = new();
    private readonly MapGenerationCoordinator _generation;
    private readonly ExplorationSession _exploration;
    private readonly SessionFlow _flow;

    private List<CardSignature> _mapSeeds = new();
    private List<CardSignature> _abilityCards = new();
    private IMapGenerator? _mapGenerator;

    public GameSessionService()
    {
        _generation = new MapGenerationCoordinator(this, _services, _world, _eventAdapter, _generationTracker);
        _exploration = new ExplorationSession(_world, _eventAdapter);
        _flow = new SessionFlow(this, _world, _eventAdapter, _exploration);
        ForwardGenerationEvents();
        ForwardExplorationEvents();
    }

    public SessionState CurrentState => _flow.State;

    public event Action<SessionState>? StateChanged;
    public event Action<SimpleMapData>? MapGenerated;
    public event Action<IGeneratedMap>? GeneratedMapReady;
    public event Action<List<CardSignature>>? LootGenerated;
    public event Action<Vector2I>? PlayerMoved;
    public event Action<Vector2>? PlayerMovedWorld;
    public event Action<Vector2I>? EnemyDefeated;
    public event Action<int>? EnemyDefeatedCell;
    public event Action<IReadOnlySet<Vector2I>>? VisitedTilesUpdated;
    public event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    public event Action<IReadOnlySet<Vector2I>, IReadOnlySet<Vector2I>>? VisibilityUpdated;
    public event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;
    public event Action<IReadOnlyList<Vector2I>, Vector2I?>? PathUpdated;
    public event Action<IReadOnlyList<int>, int?>? PathCellsUpdated;
    public event Action<float>? ProgressUpdated;

    /// <summary>
    /// Exposes the current map generation task for testing purposes.
    /// Tests can await this to wait for actual async completion instead of polling with timeouts.
    /// </summary>
    public Task? CurrentGenerationTask => _generationTracker.CurrentTask;

    /// <summary>
    /// Gets the current generated map (available after map generation completes).
    /// Works with both regular grid and irregular mesh maps.
    /// </summary>
    public IGeneratedMap? CurrentGeneratedMap => _world.GeneratedMap;

    /// <summary>
    /// Gets the current map data adapter for pathfinding and exploration.
    /// </summary>
    public IMapData? CurrentMapData => _world.MapData;

    /// <summary>
    /// Sets a custom map generator for the next session.
    /// When set, this generator will be used instead of the default SimpleMapGenerator.
    /// Set to null to use the default generator.
    /// </summary>
    /// <param name="generator">The map generator to use, or null for default.</param>
    public void SetMapGenerator(IMapGenerator? generator)
    {
        _mapGenerator = generator;
        ILog.Print(generator != null
            ? $"Map generator set to: {generator.GetType().Name}"
            : "Map generator reset to default");
    }

    public void StartSession(List<CardSignature>? mapSeeds, List<CardSignature>? abilityCards)
    {
        if (CurrentState != SessionState.WaitingForCards)
        {
            ILog.Error("Cannot start session - session already in progress");
            return;
        }

        if (mapSeeds == null || mapSeeds.Count == 0 || abilityCards == null || abilityCards.Count == 0)
        {
            ILog.Error("Cannot start session with null or empty inputs");
            return;
        }

        _mapSeeds = new List<CardSignature>(mapSeeds);
        _abilityCards = new List<CardSignature>(abilityCards);
        var generationId = _generationTracker.BeginPending();
        _flow.State = SessionState.GeneratingMap;

        ILog.Print($"Started session with {_mapSeeds.Count} map seed(s) and {_abilityCards.Count} ability cards");

        // Start the game loop
        CallDeferred(MethodName.AdvanceSessionForGeneration, generationId);
    }

    public void AdvanceSession()
    {
        switch (CurrentState)
        {
            case SessionState.GeneratingMap:
                GenerateMap();
                break;
            case SessionState.Exploring:
                _flow.StartExploration();
                break;
            case SessionState.InCombat:
                _flow.StartCombat(_abilityCards, _mapSeeds[0]);
                break;
            case SessionState.GeneratingLoot:
                _flow.GenerateLoot(_mapSeeds[0]);
                break;
            case SessionState.SessionComplete:
                ILog.Print("Session already complete");
                break;
            default:
                ILog.Error($"Cannot advance from state: {CurrentState}");
                break;
        }
    }

    public void ResetSession()
    {
        _generationTracker.Reset();
        _mapSeeds.Clear();
        _abilityCards.Clear();
        _flow.Reset();
        _flow.State = SessionState.WaitingForCards;
        ILog.Print("Session reset");
    }

    public override void _Ready()
    {
        // Note: the world's RNG is seeded deterministically from the map seed cards during generation.
        // Do not call Randomize() here as it would make map generation non-reproducible

        // Initialize biome registry immediately
        _services.BiomeRegistry = new BiomeRegistry();
        _services.BiomeRegistry.RegisterDefaultBiomes();

        // Get services via async callback (may not be registered yet during startup)
        ServiceLocator.Get<ITileRegistry>(registry => _services.TileRegistry = registry);
        ServiceLocator.Get<ITileMetadataProvider>(provider => _services.MetadataProvider = provider);

        // Create timer for game progression
        var gameTimer = new Timer();
        AddChild(gameTimer);
        _flow.AttachTimer(gameTimer);

        ILog.Print("GameSessionService ready and initialized");
    }

    private async void GenerateMap(TaskCompletionSource<bool>? generationTaskSource = null)
    {
        var generationTask = _generation.GenerateAsync(_mapSeeds.ToArray(), _mapGenerator);
        _generationTracker.CurrentTask = generationTask;
        try
        {
            await generationTask;
            generationTaskSource?.TrySetResult(true);
        }
        catch (Exception ex)
        {
            generationTaskSource?.TrySetException(ex);
            ILog.Error($"Map generation task failed: {ex.Message}");
        }
    }

    private void AdvanceSessionForGeneration(long generationId)
    {
        if (!_generation.IsCurrent(generationId))
            return;

        var generationTaskSource = _generationTracker.TakePending(generationId);
        if (generationTaskSource != null)
        {
            GenerateMap(generationTaskSource);
            return;
        }

        AdvanceSession();
    }

    private void ResetForNextSession()
    {
        _flow.State = SessionState.WaitingForCards;
        ILog.Print("Ready for next session");
    }

    private void ForwardGenerationEvents()
    {
        _flow.StateChanged += state => StateChanged?.Invoke(state);
        _flow.LootGenerated += loot => LootGenerated?.Invoke(loot);
        _generation.MapGenerated += map => MapGenerated?.Invoke(map);
        _generation.GeneratedMapReady += map => GeneratedMapReady?.Invoke(map);
        _generation.ProgressUpdated += value => ProgressUpdated?.Invoke(value);
        _generation.Succeeded += generationId =>
        {
            _flow.State = SessionState.Exploring;
            CallDeferred(MethodName.AdvanceSessionForGeneration, generationId);
        };
        _generation.Abandoned += () => _flow.State = SessionState.WaitingForCards;
    }

    private void ForwardExplorationEvents()
    {
        _exploration.PlayerMoved += position => PlayerMoved?.Invoke(position);
        _exploration.PlayerMovedWorld += position => PlayerMovedWorld?.Invoke(position);
        _exploration.EnemyDefeated += position => EnemyDefeated?.Invoke(position);
        _exploration.EnemyDefeatedCell += cellId => EnemyDefeatedCell?.Invoke(cellId);
        _exploration.VisitedTilesUpdated += tiles => VisitedTilesUpdated?.Invoke(tiles);
        _exploration.VisitedCellsUpdated += cells => VisitedCellsUpdated?.Invoke(cells);
        _exploration.VisibilityUpdated += (seen, visible) => VisibilityUpdated?.Invoke(seen, visible);
        _exploration.VisibilityCellsUpdated += (seen, visible) => VisibilityCellsUpdated?.Invoke(seen, visible);
        _exploration.PathUpdated += (path, target) => PathUpdated?.Invoke(path, target);
        _exploration.PathCellsUpdated += (path, target) => PathCellsUpdated?.Invoke(path, target);
    }
}
