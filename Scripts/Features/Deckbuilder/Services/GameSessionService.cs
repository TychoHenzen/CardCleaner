using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.AutoTiling;
using CardCleaner.Scripts.Features.Worldgen.Biomes;
using CardCleaner.Scripts.Features.Worldgen.Wfc;
using Godot;
using Timer = Godot.Timer;

// IMapGenerator support for pluggable map generation (regular grid or irregular mesh)

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

[Service(ServiceLifetime.Singleton, typeof(IGameSessionService))]
public partial class GameSessionService : Node, IGameSessionService
{
    // Game timing
    private const float ExplorationStepDelay = 0.1f; // 500ms between exploration steps
    private const float CombatTurnDelay = 1.0f; // 1s between combat turns
    private List<CardSignature> _abilityCards = new();
    private BiomeRegistry _biomeRegistry = null!;
    private SimpleCombatSystem? _combatSystem;
    private Vector2I? _currentEnemyPosition;

    // Simple game systems (backward compatible)
    private SimpleMapData? _currentMap;
    private RegularGridMapData? _currentGridMapData;
    private SessionState _currentState = SessionState.WaitingForCards;
    private ExplorationAI? _explorationAI;
    private Timer _gameTimer = null!;
    private List<CardSignature> _mapSeeds = new();

    // Pluggable map generator support
    private IMapGenerator? _mapGenerator;
    private IGeneratedMap? _generatedMap;
    private IMapData? _currentMapData;

    // Player and enemy tracking
    private Vector2? _playerWorldPosition;
    private Vector2I? _playerPosition;
    private int? _currentEnemyCellId;
    private RandomNumberGenerator _rng = new();
    private ITileRegistry _tileRegistry = null!;
    private ITileMetadataProvider _metadataProvider = null!;
    private CancellationTokenSource? _generationCts;
    private Task? _currentGenerationTask;

    public SessionState CurrentState
    {
        get => _currentState;
        private set
        {
            if (_currentState == value)
                return;
            _currentState = value;
            StateChanged?.Invoke(value);
            ILog.Print($"Session state changed to: {value}");
        }
    }

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
    public Task? CurrentGenerationTask => _currentGenerationTask;

    /// <summary>
    /// Gets the current generated map (available after map generation completes).
    /// Works with both regular grid and irregular mesh maps.
    /// </summary>
    public IGeneratedMap? CurrentGeneratedMap => _generatedMap;

    /// <summary>
    /// Gets the current map data adapter for pathfinding and exploration.
    /// </summary>
    public IMapData? CurrentMapData => _currentMapData;

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
        _currentGenerationTask = null; // Clear any stale task from previous session
        CurrentState = SessionState.GeneratingMap;

        ILog.Print($"Started session with {_mapSeeds.Count} map seed(s) and {_abilityCards.Count} ability cards");

        // Start the game loop
        CallDeferred(MethodName.AdvanceSession);
    }

    public void AdvanceSession()
    {
        switch (CurrentState)
        {
            case SessionState.GeneratingMap:
                GenerateMap();
                break;
            case SessionState.Exploring:
                StartExploration();
                break;
            case SessionState.InCombat:
                StartCombat();
                break;
            case SessionState.GeneratingLoot:
                GenerateLoot();
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
        _gameTimer.Stop();
        _mapSeeds.Clear();
        _abilityCards.Clear();
        _currentMap = null!;
        _currentGridMapData = null;
        _generatedMap = null;
        _currentMapData = null;
        _explorationAI = null!;
        _combatSystem = null!;
        _playerPosition = null;
        _playerWorldPosition = null;
        _currentEnemyPosition = null;
        _currentEnemyCellId = null;
        _currentGenerationTask = null;
        CurrentState = SessionState.WaitingForCards;
        ILog.Print("Session reset");
    }

    public override void _Ready()
    {
        // Note: _rng is seeded deterministically in GenerateMap() using ComputeSeedFromCards()
        // Do not call _rng.Randomize() here as it would make map generation non-reproducible

        // Initialize biome registry immediately
        _biomeRegistry = new BiomeRegistry();
        _biomeRegistry.RegisterDefaultBiomes();

        // Get services via async callback (may not be registered yet during startup)
        ServiceLocator.Get<ITileRegistry>(registry => _tileRegistry = registry);
        ServiceLocator.Get<ITileMetadataProvider>(provider => _metadataProvider = provider);

        // Create timer for game progression
        _gameTimer = new Timer();
        AddChild(_gameTimer);
        _gameTimer.Timeout += OnTimerTimeout;

        ILog.Print("GameSessionService ready and initialized");
    }

    private async void GenerateMap()
    {
        // Capture the task synchronously to avoid race conditions in tests
        // Task remains set after completion so tests can await it and check status
        _currentGenerationTask = GenerateMapAsync();
        await _currentGenerationTask;
    }

    private async Task GenerateMapAsync()
    {
        ILog.Print($"Starting async map generation from {_mapSeeds.Count} seed signature(s)...");

        // Cancel any previous generation in progress
        _generationCts?.Cancel();
        _generationCts?.Dispose();
        _generationCts = new CancellationTokenSource();

        // Create progress reporter for loading UI
        var progress = new GodotProgress();
        AddChild(progress);
        progress.ProgressUpdated += OnMapGenerationProgress;

        try
        {
            // Compute deterministic seed from card signatures
            var seed = ComputeSeedFromCards(_mapSeeds);
            _rng.Seed = seed;
            ILog.Print($"Map seed: {seed}");

            // Use first signature to influence map size (could blend in future)
            var mapSize = CalculateMapSize(_mapSeeds[0]);

            // Check if a custom map generator is set
            if (_mapGenerator != null)
            {
                // Use the pluggable map generator (e.g., IrregularMeshMapGenerator)
                await GenerateWithCustomGenerator(seed, mapSize, progress);
            }
            else
            {
                // Use the default SimpleMapGenerator
                await GenerateWithDefaultGenerator(seed, mapSize, progress);
            }

            CurrentState = SessionState.Exploring;
            CallDeferred(MethodName.AdvanceSession);
        }
        catch (MapGenerationCancelledException ex)
        {
            ILog.Warning($"Map generation cancelled: {ex.Message}");
            CurrentState = SessionState.WaitingForCards;
        }
        catch (OperationCanceledException ex)
        {
            ILog.Warning($"Map generation cancelled: {ex.Message}");
            CurrentState = SessionState.WaitingForCards;
        }
        catch (Exception ex)
        {
            ILog.Error($"Map generation failed: {ex.Message}");
            ILog.Error($"Stack trace: {ex.StackTrace}");
            CurrentState = SessionState.WaitingForCards;
        }
        finally
        {
            progress.QueueFree();
            _generationCts?.Dispose();
            _generationCts = null;
        }
    }

    private async Task GenerateWithCustomGenerator(ulong seed, Vector2I mapSize, IProgress<float> progress)
    {
        // Create generation config from current session state
        var config = new MapGenerationConfig
        {
            Size = mapSize,
            Seed = seed,
            MapSeeds = _mapSeeds.ToArray(),
            BiomeRegistry = _biomeRegistry
        };

        // Generate using the custom generator
        _generatedMap = await _mapGenerator!.GenerateAsync(config, progress, _generationCts!.Token);
        _currentMapData = _generatedMap.GetMapData();

        // Clear regular grid fields (not used with custom generator)
        _currentMap = null;
        _currentGridMapData = null;

        ILog.Print($"Custom map generated: {_generatedMap.EnemyCount} enemies");

        // Notify listeners with the new event
        GeneratedMapReady?.Invoke(_generatedMap);
    }

    private async Task GenerateWithDefaultGenerator(ulong seed, Vector2I mapSize, IProgress<float> progress)
    {
        // Ensure tile registry is available (fallback if async callback hasn't run yet)
        _tileRegistry ??= new TileRegistry();

        // Create gradient from all map seeds for biome placement
        var gradient = new CardBasedGradient(_mapSeeds.ToArray(), _rng);

        // Create biome provider that maps gradient signatures to biomes
        var biomeProvider = new BiomeMapGenerator(_biomeRegistry, gradient, mapSize);

        // Create WFC generator with hard constraints (2x2 window, adjacency rules)
        // Pass tile registry so WfcMapGenerator uses TileDefinition.IsPassable for connectivity
        var transitionResolver = new CompiledTransitionResolver();
        var wfcGenerator = new WfcMapGenerator(transitionResolver, _tileRegistry);

        // Create map generator with WFC for terrain generation
        var mapGenerator = new SimpleMapGenerator(
            _rng, biomeProvider, _tileRegistry, _metadataProvider, wfcGenerator, _biomeRegistry, gradient);

        // Wrap in async adapter and generate on background thread
        var asyncGenerator = new AsyncMapGeneratorAdapter(mapGenerator);
        _currentMap = await asyncGenerator.GenerateMapAsync(mapSize, progress, _generationCts!.Token);

        // Create the IMapData adapter for exploration
        _currentGridMapData = new RegularGridMapData(_currentMap);
        _currentMapData = _currentGridMapData;

        // Wrap in IGeneratedMap for unified interface
        _generatedMap = new SimpleGeneratedMap(_currentMap);

        // Log biome distribution for debugging
        biomeProvider.LogBiomeStats();

        ILog.Print($"Map generated: {mapSize.X}x{mapSize.Y}, {_currentMap.EnemyPositions.Count} enemies");

        // Notify listeners about the generated map (backward compatible event)
        MapGenerated?.Invoke(_currentMap);
        GeneratedMapReady?.Invoke(_generatedMap);
    }

    private void OnMapGenerationProgress(float value)
    {
        ProgressUpdated?.Invoke(value);
        ILog.Print($"Map generation: {value * 100:F0}%");
    }

    private void StartExploration()
    {
        var enemyCount = _generatedMap?.EnemyCount ?? _currentMap?.EnemyPositions.Count ?? 0;
        ILog.Print($"Starting exploration... ({enemyCount} enemies on map)");

        // Check if we have valid map data (from either regular or custom generator)
        if (_currentMapData == null)
        {
            ILog.Error("Cannot start exploration - no map data available");
            return;
        }

        // Determine starting cell ID based on available data
        int? startCellId = null;
        if (_playerWorldPosition.HasValue)
        {
            // Resume from world position (custom generator case)
            startCellId = _currentMapData.GetCellAtPosition(_playerWorldPosition.Value);
        }
        else if (_playerPosition.HasValue && _currentGridMapData != null)
        {
            // Resume from grid position (regular generator case)
            startCellId = _currentGridMapData.GetCellId(_playerPosition.Value);
        }

        _explorationAI = new ExplorationAI(_currentMapData, startCellId);

        // Subscribe with adapters to emit both Vector2I and cell-based events
        _explorationAI.EnemyEncountered += OnEnemyEncounteredWorld;
        _explorationAI.PlayerMoved += OnPlayerMovedWorld;
        _explorationAI.VisitedCellsUpdated += OnVisitedCellsUpdated;
        _explorationAI.VisibilityUpdated += OnVisibilityUpdated;
        _explorationAI.PathUpdated += OnPathUpdated;

        // Start exploration timer
        _gameTimer.WaitTime = ExplorationStepDelay;
        _gameTimer.Start();
    }

    // Adapter methods to emit both Vector2I (backward compat) and cell-based events

    private void OnPlayerMovedWorld(Vector2 worldPos)
    {
        // Track world position for custom generators
        _playerWorldPosition = worldPos;

        // Emit world position event (for irregular mesh)
        PlayerMovedWorld?.Invoke(worldPos);

        // Emit grid position event (backward compat for regular grid)
        if (_currentGridMapData != null)
        {
            var gridPos = _currentGridMapData.WorldToGrid(worldPos);
            _playerPosition = gridPos;
            PlayerMoved?.Invoke(gridPos);
        }
    }

    private void OnEnemyEncounteredWorld(Vector2 worldPos)
    {
        // Get cell ID for the enemy position
        var cellId = _currentMapData?.GetCellAtPosition(worldPos);
        if (cellId.HasValue)
        {
            _currentEnemyCellId = cellId.Value;
        }

        // For regular grid, also convert to grid position
        if (_currentGridMapData != null)
        {
            var gridPos = _currentGridMapData.WorldToGrid(worldPos);
            OnEnemyEncountered(gridPos);
        }
        else
        {
            // Custom generator - just log and start combat
            ILog.Print($"Enemy encountered at world position {worldPos}! Preparing for combat...");
            _gameTimer.Stop();
            CurrentState = SessionState.InCombat;
            CallDeferred(MethodName.AdvanceSession);
        }
    }

    private void OnVisitedCellsUpdated(IReadOnlySet<int> cellIds)
    {
        // Emit cell-based event (for irregular mesh)
        VisitedCellsUpdated?.Invoke(cellIds);

        // Emit grid position event (backward compat for regular grid)
        if (_currentGridMapData != null)
        {
            var gridPositions = new HashSet<Vector2I>(cellIds.Select(id => _currentGridMapData.GetGridPosition(id)));
            VisitedTilesUpdated?.Invoke(gridPositions);
        }
    }

    private void OnVisibilityUpdated(IReadOnlySet<int> seenCells, IReadOnlySet<int> visibleCells)
    {
        // Emit cell-based event (for irregular mesh)
        VisibilityCellsUpdated?.Invoke(seenCells, visibleCells);

        // Emit grid position event (backward compat for regular grid)
        if (_currentGridMapData != null)
        {
            var seenPositions = new HashSet<Vector2I>(seenCells.Select(id => _currentGridMapData.GetGridPosition(id)));
            var visiblePositions = new HashSet<Vector2I>(visibleCells.Select(id => _currentGridMapData.GetGridPosition(id)));
            VisibilityUpdated?.Invoke(seenPositions, visiblePositions);
        }
    }

    private void OnPathUpdated()
    {
        if (_explorationAI == null) return;

        // Emit cell-based event (for irregular mesh)
        PathCellsUpdated?.Invoke(_explorationAI.CurrentPath, _explorationAI.CurrentTargetCell);

        // Emit grid position event (backward compat for regular grid)
        if (_currentGridMapData != null)
        {
            var pathPositions = _explorationAI.CurrentPath
                .Select(id => _currentGridMapData.GetGridPosition(id))
                .ToList();

            var targetPosition = _explorationAI.CurrentTargetCell.HasValue
                ? _currentGridMapData.GetGridPosition(_explorationAI.CurrentTargetCell.Value)
                : (Vector2I?)null;

            PathUpdated?.Invoke(pathPositions, targetPosition);
        }
    }

    private void OnTimerTimeout()
    {
        switch (CurrentState)
        {
            case SessionState.Exploring:
                ProcessExplorationStep();
                break;
            case SessionState.InCombat:
                ProcessCombatTurn();
                break;
        }
    }

    private void ProcessExplorationStep()
    {
        if (_explorationAI == null) return;

        var shouldContinue = _explorationAI.StepExploration();

        if (!shouldContinue)
        {
            _gameTimer.Stop();

            if (_explorationAI.HasFoundEnemy)
            {
                CurrentState = SessionState.InCombat;
                CallDeferred(MethodName.AdvanceSession);
            }
            else
            {
                ILog.Print("Exploration complete - no enemies found, ending session");
                CurrentState = SessionState.GeneratingLoot;
                CallDeferred(MethodName.AdvanceSession);
            }
        }
    }

    private void OnEnemyEncountered(Vector2I position)
    {
        ILog.Print($"Enemy encountered at {position}! Preparing for combat...");
        _currentEnemyPosition = position;
        _gameTimer.Stop();
        CurrentState = SessionState.InCombat;
        CallDeferred(MethodName.AdvanceSession);
    }

    private void StartCombat()
    {
        ILog.Print($"Starting combat with {_abilityCards.Count} ability cards...");

        _combatSystem = new SimpleCombatSystem(_abilityCards, _mapSeeds[0], _rng);
        _combatSystem.CombatEnded += OnCombatEnded;

        // Start combat timer
        _gameTimer.WaitTime = CombatTurnDelay;
        _gameTimer.Start();
    }

    private void ProcessCombatTurn()
    {
        if (_combatSystem == null) return;

        var shouldContinue = _combatSystem.ProcessTurn();

        if (!shouldContinue) _gameTimer.Stop();
    }

    private void OnCombatEnded()
    {
        if (_combatSystem?.PlayerWon == true)
        {
            // Remove the defeated enemy from the map using unified interface
            if (_currentEnemyCellId.HasValue)
            {
                var defeatedCellId = _currentEnemyCellId.Value;

                // Update player position to enemy's position
                if (_currentMapData != null)
                {
                    _playerWorldPosition = _currentMapData.GetCellCenter(defeatedCellId);
                }

                // Remove enemy using IGeneratedMap interface
                var removed = _generatedMap?.RemoveEnemyAt(defeatedCellId) ?? false;
                var remainingEnemies = _generatedMap?.EnemyCount ?? 0;

                ILog.Print($"Enemy at cell {defeatedCellId} destroyed! ({remainingEnemies} enemies remaining)");

                // Notify UI to remove enemy sprite (cell-based event)
                EnemyDefeatedCell?.Invoke(defeatedCellId);
                _currentEnemyCellId = null;
            }

            // Also handle backward-compat grid position event
            if (_currentEnemyPosition.HasValue)
            {
                var defeatedPosition = _currentEnemyPosition.Value;
                _playerPosition = defeatedPosition;
                _currentMap?.EnemyPositions.Remove(defeatedPosition);
                EnemyDefeated?.Invoke(defeatedPosition);
                _currentEnemyPosition = null;
            }

            // Check if more enemies remain on the map
            var hasMoreEnemies = (_generatedMap?.EnemyCount ?? _currentMap?.EnemyPositions.Count ?? 0) > 0;
            if (hasMoreEnemies)
            {
                ILog.Print($"Resuming exploration to find remaining enemies...");
                CurrentState = SessionState.Exploring;
                CallDeferred(MethodName.AdvanceSession);
            }
            else
            {
                ILog.Print("All enemies destroyed! Generating loot...");
                CurrentState = SessionState.GeneratingLoot;
                CallDeferred(MethodName.AdvanceSession);
            }
        }
        else
        {
            ILog.Print("Combat lost! Session ending...");
            CleanupCurrentSession();
            CurrentState = SessionState.SessionComplete;
            CallDeferred(MethodName.ResetForNextSession);
        }
    }

    private void GenerateLoot()
    {
        ILog.Print("Generating loot from defeated enemy...");

        var lootSignatures = new List<CardSignature>();

        // Generate 5-10 cards based on map seed and abilities used
        var lootCount = _rng.RandiRange(5, 10);

        for (var i = 0; i < lootCount; i++)
        {
            var lootSignature = GenerateLootSignature();
            lootSignatures.Add(lootSignature);
        }

        CleanupCurrentSession();

        CurrentState = SessionState.SessionComplete;
        LootGenerated?.Invoke(lootSignatures);

        ILog.Print($"Session complete! Generated {lootSignatures.Count} loot cards");
        CallDeferred(MethodName.ResetForNextSession);
    }

    private void CleanupCurrentSession()
    {
        // Stop any running timers
        _gameTimer?.Stop();

        // Clean up GameSessionService's own data
        _currentMap = null!;
        _currentGridMapData = null;
        _generatedMap = null;
        _currentMapData = null;
        _combatSystem = null!;
        _explorationAI = null!;
        _playerPosition = null;
        _playerWorldPosition = null;
        _currentEnemyPosition = null;
        _currentEnemyCellId = null;
    }

    private void ResetForNextSession()
    {
        CurrentState = SessionState.WaitingForCards;
        ILog.Print("Ready for next session");
    }


    private CardSignature GenerateLootSignature()
    {
        // Create signature that's a variation of the map seed plus random elements from abilities
        var lootSignature = new CardSignature();
        var baseSeed = _mapSeeds[0];

        for (var i = 0; i < 8; i++)
        {
            var variation = _rng.Randfn(baseSeed[i], 0.1f);
            lootSignature[i] = Mathf.Clamp(variation, -1f, 1f);
        }

        return lootSignature;
    }

    private Vector2I CalculateMapSize(CardSignature signature)
    {
        // Use signature to determine map size (larger for more complex signatures)
        var complexity = 0f;
        for (var i = 0; i < 8; i++) complexity += Mathf.Abs(signature[i]);
        complexity /= 8f;

        var baseSize = 50;
        var sizeVariation = Mathf.RoundToInt(complexity * 25);
        var size = baseSize + sizeVariation;

        return new Vector2I(size, size);
    }

    /// <summary>
    /// Computes a deterministic seed from a list of card signatures.
    /// Same cards in same order always produce the same seed.
    /// </summary>
    internal static ulong ComputeSeedFromCards(List<CardSignature> cards)
    {
        var hash = 17UL;
        foreach (var card in cards)
        {
            for (var i = 0; i < 8; i++)
            {
                // Use BitConverter for deterministic float→bits conversion
                hash = hash * 31 + BitConverter.ToUInt32(BitConverter.GetBytes(card[i]), 0);
            }
        }
        return hash;
    }
}
