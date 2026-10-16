using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session.Generation;
using Godot;
using Timer = Godot.Timer;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

[Service(typeof(IGameSessionService))]
public partial class GameSessionService : Node, IGameSessionService
{
    private readonly ISessionWorld _world = SessionWorlds.Create();
    private readonly GenerationTracker _generationTracker = new();
    private readonly MapGenerationCoordinator _generation;
    private readonly IExplorationSession _exploration;
    private readonly SessionFlow _flow;

    private List<CardSignature> _mapSeeds = new();
    private List<CardSignature> _abilityCards = new();
    private IMapGenerator? _mapGenerator;

    public GameSessionService()
    {
        _generation = new MapGenerationCoordinator(this, _world, _generationTracker);
        _exploration = ExplorationSessions.Create(_world);
        _flow = new SessionFlow(this, _world, _exploration);
        ForwardGenerationEvents();
        ForwardExplorationEvents();
    }

    public SessionState CurrentState => _flow.State;

    public event Action<SessionState>? StateChanged;
    public event Action<IGeneratedMap>? GeneratedMapReady;
    public event Action<List<CardSignature>>? LootGenerated;
    public event Action<Vector2>? PlayerMovedWorld;
    public event Action<int>? EnemyDefeatedCell;
    public event Action<IReadOnlySet<int>>? VisitedCellsUpdated;
    public event Action<IReadOnlySet<int>, IReadOnlySet<int>>? VisibilityCellsUpdated;
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

    public void StartSession(
        List<CardSignature>? mapSeeds,
        List<CardSignature>? abilityCards,
        IMapGenerator mapGenerator,
        IVisibilityChecker? visibilityChecker = null)
    {
        ArgumentNullException.ThrowIfNull(mapGenerator);

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
        _mapGenerator = mapGenerator;
        _exploration.VisibilityChecker = visibilityChecker;
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
        _mapGenerator = null;
        _exploration.VisibilityChecker = null;
        _flow.Reset();
        _flow.State = SessionState.WaitingForCards;
        ILog.Print("Session reset");
    }

    public void ReportStepShown() => _flow.ReportStepShown();

    public override void _Ready()
    {
        // Note: the world's RNG is seeded deterministically from the map seed cards during generation.
        // Do not call Randomize() here as it would make map generation non-reproducible

        // Create timer for game progression
        var gameTimer = new Timer();
        AddChild(gameTimer);
        _flow.AttachTimer(gameTimer);

        ILog.Print("GameSessionService ready and initialized");
    }

    private async void GenerateMap(TaskCompletionSource<bool>? generationTaskSource = null)
    {
        if (_mapGenerator is not { } generator)
        {
            const string message = "Cannot generate a map before a session has started";
            ILog.Error(message);
            generationTaskSource?.TrySetException(new InvalidOperationException(message));
            return;
        }

        var generationTask = _generation.GenerateAsync(_mapSeeds.ToArray(), generator);
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
        _exploration.PlayerMovedWorld += position => PlayerMovedWorld?.Invoke(position);
        _exploration.EnemyDefeatedCell += cellId => EnemyDefeatedCell?.Invoke(cellId);
        _exploration.VisitedCellsUpdated += cells => VisitedCellsUpdated?.Invoke(cells);
        _exploration.VisibilityCellsUpdated += (seen, visible) => VisibilityCellsUpdated?.Invoke(seen, visible);
        _exploration.PathCellsUpdated += (path, target) => PathCellsUpdated?.Invoke(path, target);
    }
}
