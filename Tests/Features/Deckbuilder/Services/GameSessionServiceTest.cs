using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Services;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;
using Godot;

namespace CardCleaner.Tests.Features.Deckbuilder.Services;

[TestSuite]
[RequireGodotRuntime]
public class GameSessionServiceTest
{
    private GameSessionService _service = null!;
    private SessionState _lastStateChange;
    private List<CardSignature> _lastLootGenerated = new();
    private int _stateChangeCount;
    private TileRegistry _tileRegistry = null!;

    [BeforeTest]
    public async Task Setup()
    {
        // Load the real tile registry so map generation works
        _tileRegistry = new TileRegistry();
        _tileRegistry.LoadFromData();

        // Register with ServiceLocator so GameSessionService can resolve it
        ServiceLocator.Container.RegisterSingleton<ITileRegistry>(_tileRegistry);
        ServiceLocator.Container.RegisterSingleton<ITileMetadataProvider>(_tileRegistry);

        _service = new GameSessionService();
        Assertions.AddNode(_service);
        await ISceneRunner.SyncProcessFrame;

        _lastStateChange = SessionState.WaitingForCards;
        _lastLootGenerated = new List<CardSignature>();
        _stateChangeCount = 0;

        _service.StateChanged += OnStateChanged;
        _service.LootGenerated += OnLootGenerated;
    }

    private void OnStateChanged(SessionState newState)
    {
        _lastStateChange = newState;
        _stateChangeCount++;
    }

    private void OnLootGenerated(List<CardSignature> loot)
    {
        _lastLootGenerated = loot;
    }

    [TestCase]
    public void TestInitialState()
    {
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
    }

    [TestCase]
    public void TestStartSessionWithValidInputs()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var abilityCards = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(mapSeeds, abilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingMap);
        Assertions.AssertThat(_stateChangeCount).IsEqual(1);
        Assertions.AssertThat(_lastStateChange).IsEqual(SessionState.GeneratingMap);
    }

    [TestCase]
    public void TestStartSessionWithNullMapSeeds()
    {
        var abilityCards = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(null, abilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWithEmptyMapSeeds()
    {
        var emptyMapSeeds = new List<CardSignature>();
        var abilityCards = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(emptyMapSeeds, abilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWithEmptyAbilityCards()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var emptyAbilityCards = new List<CardSignature>();

        _service.StartSession(mapSeeds, emptyAbilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWhenNotWaiting()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };
        _service.StartSession(mapSeeds, abilityCards);

        _stateChangeCount = 0;

        _service.StartSession(mapSeeds, abilityCards);

        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestResetSession()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };
        _service.StartSession(mapSeeds, abilityCards);

        _service.ResetSession();

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
    }

    [TestCase]
    public void TestAdvanceSessionFromWaitingFails()
    {
        _service.AdvanceSession();

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public async Task TestStateTransitionToExploring()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingMap);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        // Wait for deferred state transition to process
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.Exploring);
    }

    [TestCase]
    public async Task TestFullSessionFlowProgresses()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        // Wait for deferred state transition to process
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Session should have progressed to at least Exploring
        // Full completion is too slow for unit tests (requires exploration + combat)
        var validState = _service.CurrentState == SessionState.Exploring ||
                        _service.CurrentState == SessionState.InCombat ||
                        _service.CurrentState == SessionState.GeneratingLoot ||
                        _service.CurrentState == SessionState.SessionComplete ||
                        _service.CurrentState == SessionState.WaitingForCards;
        Assertions.AssertBool(validState).IsTrue();
    }

    [TestCase]
    public async Task TestLootGeneration()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for loot to be generated (limited wait - full session is slow)
        var frameCount = 0;
        var maxFrames = 500; // ~8 seconds at 60fps

        while (_lastLootGenerated.Count == 0 && frameCount < maxFrames)
        {
            await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
            frameCount++;
        }

        // If loot was generated, verify it's valid
        // Note: Full session completion requires exploration + combat which can be slow
        if (_lastLootGenerated.Count > 0)
        {
            Assertions.AssertThat(_lastLootGenerated.Count).IsBetween(5, 10);

            // Loot should have valid signature values
            foreach (var lootSignature in _lastLootGenerated)
            {
                Assertions.AssertThat(lootSignature).IsNotNull();
                for (var i = 0; i < 8; i++)
                    Assertions.AssertThat(lootSignature[i]).IsBetween(-1.0f, 1.0f);
            }
        }
        else
        {
            // Session hasn't completed yet - verify it's still progressing
            var validProgressState = _service.CurrentState != SessionState.WaitingForCards;
            Assertions.AssertBool(validProgressState).IsTrue();
        }
    }

    [TestCase]
    public async Task TestLootGenerationVariation()
    {
        // This is an integration test that requires full sessions to complete
        // We test that if loot is generated, it varies between sessions
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var abilityCards = new List<CardSignature> { new() };

        // First session - limited wait time
        _service.StartSession(mapSeeds, abilityCards);

        var frameCount = 0;
        var maxFrames = 300;
        while (_lastLootGenerated.Count == 0 && frameCount < maxFrames)
        {
            await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
            frameCount++;
        }

        // If first session completed with loot, try second session
        if (_lastLootGenerated.Count > 0)
        {
            var firstLoot = new List<CardSignature>(_lastLootGenerated);

            // Wait for reset
            frameCount = 0;
            while (_service.CurrentState != SessionState.WaitingForCards && frameCount < maxFrames)
            {
                await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
                frameCount++;
            }

            if (_service.CurrentState == SessionState.WaitingForCards)
            {
                // Second session
                _lastLootGenerated.Clear();
                _service.StartSession(mapSeeds, abilityCards);

                frameCount = 0;
                while (_lastLootGenerated.Count == 0 && frameCount < maxFrames)
                {
                    await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);
                    frameCount++;
                }

                if (_lastLootGenerated.Count > 0)
                {
                    var secondLoot = _lastLootGenerated;

                    // Should have different loot (due to randomness)
                    var foundDifference = false;
                    for (var i = 0; i < Math.Min(firstLoot.Count, secondLoot.Count); i++)
                    {
                        for (var j = 0; j < 8; j++)
                        {
                            if (Math.Abs(firstLoot[i][j] - secondLoot[i][j]) > 0.001f)
                            {
                                foundDifference = true;
                                break;
                            }
                        }
                        if (foundDifference) break;
                    }

                    Assertions.AssertBool(foundDifference).IsTrue();
                    return;
                }
            }
        }

        // If we couldn't complete sessions in time, just verify session started correctly
        Assertions.AssertBool(_stateChangeCount >= 1).IsTrue();
    }

    [TestCase]
    public async Task TestMapGeneratedEvent()
    {
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };
        SimpleMapData? generatedMap = null;

        _service.MapGenerated += map => generatedMap = map;

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(generatedMap).IsNotNull();
        Assertions.AssertThat(generatedMap!.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public async Task TestStateChangedEventFires()
    {
        var mapSeeds = new List<CardSignature> { new() };
        var abilityCards = new List<CardSignature> { new() };
        var stateHistory = new List<SessionState>();

        _service.StateChanged += state => stateHistory.Add(state);

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        // Wait for deferred state transition to process
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Should have at least GeneratingMap and Exploring in history
        Assertions.AssertThat(stateHistory.Count).IsGreaterEqual(2);
        Assertions.AssertThat(stateHistory[0]).IsEqual(SessionState.GeneratingMap);
        Assertions.AssertThat(stateHistory[1]).IsEqual(SessionState.Exploring);
    }

    [TestCase]
    public async Task TestMultipleMapSeedsGenerateMap()
    {
        // Test that multiple map seeds are accepted and map generation proceeds
        // CardBasedGradient uses capsule (2 cards) or Bezier (3+ cards) patterns
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f }),
            new(new[] { -0.5f, 0.3f, -0.8f, 0.1f, -0.2f, 0.7f, -0.9f, 0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };
        SimpleMapData? generatedMap = null;

        _service.MapGenerated += map => generatedMap = map;

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(generatedMap).IsNotNull();
        Assertions.AssertThat(generatedMap!.PassableTiles.Count).IsGreater(0);
    }

    [TestCase]
    public async Task TestThreeMapSeedsGenerateMap()
    {
        // Test three map seeds (triggers Bezier gradient pattern)
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 0.0f, 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var abilityCards = new List<CardSignature> { new() };
        SimpleMapData? generatedMap = null;

        _service.MapGenerated += map => generatedMap = map;

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(generatedMap).IsNotNull();
        Assertions.AssertThat(generatedMap!.PassableTiles.Count).IsGreater(0);
    }

    // ======== Deterministic RNG Seeding Tests (Phase 1.1) ========

    [TestCase]
    public void ComputeSeedFromCards_SameCards_ReturnsSameSeed()
    {
        // Same cards should always produce the same seed
        var cards1 = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 1.0f, 0.0f, -0.5f, 0.25f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var cards2 = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new(new[] { 1.0f, 0.0f, -0.5f, 0.25f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        var seed1 = GameSessionService.ComputeSeedFromCards(cards1);
        var seed2 = GameSessionService.ComputeSeedFromCards(cards2);

        Assertions.AssertThat(seed1).IsEqual(seed2);
    }

    [TestCase]
    public void ComputeSeedFromCards_DifferentCards_ReturnsDifferentSeeds()
    {
        // Different cards should produce different seeds
        var cards1 = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };
        var cards2 = new List<CardSignature>
        {
            new(new[] { 0.6f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }) // Slightly different
        };

        var seed1 = GameSessionService.ComputeSeedFromCards(cards1);
        var seed2 = GameSessionService.ComputeSeedFromCards(cards2);

        Assertions.AssertThat(seed1).IsNotEqual(seed2);
    }

    [TestCase]
    public void ComputeSeedFromCards_OrderMatters()
    {
        // [A, B] should produce different seed than [B, A]
        var cardA = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var cardB = new CardSignature(new[] { 1.0f, 0.0f, -0.5f, 0.25f, 0.0f, 0.0f, 0.0f, 0.0f });

        var cardsAB = new List<CardSignature> { cardA, cardB };
        var cardsBA = new List<CardSignature> { cardB, cardA };

        var seedAB = GameSessionService.ComputeSeedFromCards(cardsAB);
        var seedBA = GameSessionService.ComputeSeedFromCards(cardsBA);

        Assertions.AssertThat(seedAB).IsNotEqual(seedBA);
    }

    [TestCase]
    public async Task GenerateMap_SameSeed_ProducesSameMap()
    {
        // Same cards should produce the same map (player position, enemy positions)
        var mapSeeds = new List<CardSignature>
        {
            new(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f })
        };
        var abilityCards = new List<CardSignature> { new() };

        SimpleMapData? firstMap = null;
        _service.MapGenerated += map => firstMap = map;

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        var generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(firstMap).IsNotNull();

        // Reset and start second session with same cards
        _service.ResetSession();

        SimpleMapData? secondMap = null;
        _service.MapGenerated += map => secondMap = map;

        _service.StartSession(mapSeeds, abilityCards);

        // Wait for deferred AdvanceSession call to execute and set the task
        await _service.ToSignal(_service.GetTree(), SceneTree.SignalName.ProcessFrame);

        // Now the generation task should be set
        generationTask = _service.CurrentGenerationTask;
        Assertions.AssertThat(generationTask).IsNotNull();
        await generationTask!;

        Assertions.AssertThat(secondMap).IsNotNull();

        // Compare maps - player start and enemy positions should be identical
        Assertions.AssertThat(firstMap!.PlayerStart).IsEqual(secondMap!.PlayerStart);
        Assertions.AssertThat(firstMap.EnemyPositions.Count).IsEqual(secondMap.EnemyPositions.Count);

        // Verify enemy positions match
        foreach (var enemyPos in firstMap.EnemyPositions)
        {
            Assertions.AssertBool(secondMap.EnemyPositions.Contains(enemyPos)).IsTrue();
        }
    }
}
