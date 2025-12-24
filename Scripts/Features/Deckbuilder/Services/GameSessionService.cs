using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services;

[Service(ServiceLifetime.Singleton, typeof(IGameSessionService))]
public partial class GameSessionService : Node, IGameSessionService
{
    private SessionState _currentState = SessionState.WaitingForCards;
    private CardSignature _mapSeed = null!;
    private List<CardSignature> _abilityCards = new();
    private RandomNumberGenerator _rng = new();

    // Simple game systems
    private SimpleMapGenerator _mapGenerator = null!;
    private SimpleMapData? _currentMap;
    private ExplorationAI? _explorationAI;
    private SimpleCombatSystem? _combatSystem;
    private Timer _gameTimer = null!;

    // Player and enemy tracking
    private Vector2I? _playerPosition;
    private Vector2I? _currentEnemyPosition;

    // Game timing
    private const float ExplorationStepDelay = 0.5f; // 500ms between exploration steps
    private const float CombatTurnDelay = 1.0f; // 1s between combat turns

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
    public event Action<List<CardSignature>>? LootGenerated;
    public event Action<Vector2I>? PlayerMoved;
    public event Action<Vector2I>? EnemyDefeated;
    public event Action<IReadOnlySet<Vector2I>>? VisitedTilesUpdated;
    public event Action<IReadOnlyList<Vector2I>, Vector2I?>? PathUpdated;

    public override void _Ready()
    {
        _rng.Randomize();
        _mapGenerator = new SimpleMapGenerator(_rng);

        // Create timer for game progression
        _gameTimer = new Timer();
        AddChild(_gameTimer);
        _gameTimer.Timeout += OnTimerTimeout;

        ILog.Print("GameSessionService ready and initialized");
    }

    public void StartSession(CardSignature? mapSeed, List<CardSignature>? abilityCards)
    {
        if (CurrentState != SessionState.WaitingForCards)
        {
            ILog.Error("Cannot start session - session already in progress");
            return;
        }

        if (mapSeed == null || abilityCards == null || abilityCards.Count == 0)
        {
            ILog.Error("Cannot start session with null or empty inputs");
            return;
        }

        _mapSeed = mapSeed;
        _abilityCards = new List<CardSignature>(abilityCards);
        CurrentState = SessionState.GeneratingMap;

        ILog.Print($"Started session with map seed and {_abilityCards.Count} ability cards");

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
        _mapSeed = null!;
        _abilityCards.Clear();
        _currentMap = null!;
        _explorationAI = null!;
        _combatSystem = null!;
        _playerPosition = null;
        _currentEnemyPosition = null;
        CurrentState = SessionState.WaitingForCards;
        ILog.Print("Session reset");
    }

    private void GenerateMap()
    {
        ILog.Print($"Generating simple map from seed signature: {_mapSeed}");

        // Use signature to influence map size and difficulty
        var mapSize = CalculateMapSize(_mapSeed);
        var blockedPercentage = CalculateBlockedPercentage(_mapSeed);

        _currentMap = _mapGenerator.GenerateMap(mapSize, _mapSeed, blockedPercentage);

        ILog.Print($"Map generated: {mapSize.X}x{mapSize.Y}, {_currentMap.EnemyPositions.Count} enemies");

        // Notify listeners about the generated map
        MapGenerated?.Invoke(_currentMap);

        CurrentState = SessionState.Exploring;
        CallDeferred(MethodName.AdvanceSession);
    }

    private void StartExploration()
    {
        ILog.Print($"Starting exploration... ({_currentMap?.EnemyPositions.Count ?? 0} enemies on map)");

        // Continue from current player position if resuming, otherwise start fresh
        if (_currentMap == null) return;
        _explorationAI = new ExplorationAI(_currentMap, _playerPosition);
        _explorationAI.EnemyEncountered += OnEnemyEncountered;
        _explorationAI.PlayerMoved += pos => PlayerMoved?.Invoke(pos);
        _explorationAI.VisitedTilesUpdated += tiles => VisitedTilesUpdated?.Invoke(tiles);
        _explorationAI.PathUpdated += () => PathUpdated?.Invoke(_explorationAI.CurrentPath, _explorationAI.CurrentTarget);

        // Start exploration timer
        _gameTimer.WaitTime = ExplorationStepDelay;
        _gameTimer.Start();
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

        _combatSystem = new SimpleCombatSystem(_abilityCards, _mapSeed, _rng);
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
            // Remove the defeated enemy from the map
            if (_currentEnemyPosition.HasValue)
            {
                var defeatedPosition = _currentEnemyPosition.Value;
                // Player is now at the enemy's position
                _playerPosition = defeatedPosition;
                _currentMap?.EnemyPositions.Remove(defeatedPosition);
                ILog.Print($"Enemy at {defeatedPosition} destroyed! ({_currentMap?.EnemyPositions.Count ?? 0} enemies remaining)");

                // Notify UI to remove enemy sprite
                EnemyDefeated?.Invoke(defeatedPosition);
                _currentEnemyPosition = null;
            }

            // Check if more enemies remain on the map
            if (_currentMap?.EnemyPositions.Count > 0)
            {
                ILog.Print($"Resuming exploration from {_playerPosition} to find remaining enemies...");
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
        _combatSystem = null!;
        _explorationAI = null!;
        _playerPosition = null;
        _currentEnemyPosition = null;
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

        for (var i = 0; i < 8; i++)
        {
            var variation = _rng.Randfn(_mapSeed[i], 0.1f);
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

        var baseSize = 20;
        var sizeVariation = Mathf.RoundToInt(complexity * 12);
        var size = baseSize + sizeVariation;

        return new Vector2I(size, size);
    }

    private float CalculateBlockedPercentage(CardSignature signature)
    {
        // Use specific elements to determine map density
        var solidumValue = Mathf.Abs(signature[0]); // Solidum affects terrain density
        return Mathf.Clamp(0.2f + solidumValue * 0.3f, 0.15f, 0.5f);
    }
}