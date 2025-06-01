using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using GdUnit4;

namespace CardCleaner.Tests.Features;

[TestSuite]
public class GameSessionServiceTest
{
    private GameSessionService _service;
    private SessionState _lastStateChange;
    private List<CardSignature> _lastLootGenerated;
    private int _stateChangeCount;

    [BeforeTest]
    public void Setup()
    {
        _service = new GameSessionService();
        _lastStateChange = SessionState.WaitingForCards;
        _lastLootGenerated = null;
        _stateChangeCount = 0;
        
        _service.StateChanged += OnStateChanged;
        _service.LootGenerated += OnLootGenerated;
    }

    [AfterTest]
    public void Cleanup()
    {
        _service?.QueueFree();
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
        var mapSeed = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var abilityCards = new List<CardSignature>
        {
            new CardSignature(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f }),
            new CardSignature(new[] { 0.0f, 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(mapSeed, abilityCards);

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingMap);
        Assertions.AssertThat(_stateChangeCount).IsEqual(1);
        Assertions.AssertThat(_lastStateChange).IsEqual(SessionState.GeneratingMap);
    }

    [TestCase]
    public void TestStartSessionWithNullMapSeed()
    {
        var abilityCards = new List<CardSignature>
        {
            new CardSignature(new[] { 1.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f })
        };

        _service.StartSession(null, abilityCards);

        // Should remain in waiting state
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWithEmptyAbilityCards()
    {
        var mapSeed = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var emptyAbilityCards = new List<CardSignature>();

        _service.StartSession(mapSeed, emptyAbilityCards);

        // Should remain in waiting state
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStartSessionWhenNotWaiting()
    {
        // First start a valid session
        var mapSeed = new CardSignature();
        var abilityCards = new List<CardSignature> { new CardSignature() };
        _service.StartSession(mapSeed, abilityCards);

        // Reset counters
        _stateChangeCount = 0;

        // Try to start another session
        _service.StartSession(mapSeed, abilityCards);

        // Should not change state
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestResetSession()
    {
        // Start a session first
        var mapSeed = new CardSignature();
        var abilityCards = new List<CardSignature> { new CardSignature() };
        _service.StartSession(mapSeed, abilityCards);

        // Reset the session
        _service.ResetSession();

        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
    }

    [TestCase]
    public void TestAdvanceSessionFromWaitingFails()
    {
        // Should not be able to advance from waiting state
        _service.AdvanceSession();

        // State should remain unchanged
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.WaitingForCards);
        Assertions.AssertThat(_stateChangeCount).IsEqual(0);
    }

    [TestCase]
    public void TestStateTransitionOrder()
    {
        var mapSeed = new CardSignature();
        var abilityCards = new List<CardSignature> { new CardSignature() };
        
        // Start session (should go to GeneratingMap)
        _service.StartSession(mapSeed, abilityCards);
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingMap);

        // Advance should go to Exploring (but this happens automatically via CallDeferred)
        // We can test the manual advance path
        _service.AdvanceSession();
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.Exploring);

        // Advance should go to InCombat
        _service.AdvanceSession();
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.InCombat);

        // Advance should go to GeneratingLoot
        _service.AdvanceSession();
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.GeneratingLoot);

        // Advance should go to SessionComplete
        _service.AdvanceSession();
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.SessionComplete);
    }

    [TestCase]
    public void TestLootGeneration()
    {
        var mapSeed = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, -0.1f, 0.2f, -0.7f, 0.9f, -0.4f });
        var abilityCards = new List<CardSignature> { new CardSignature() };
        
        // Start and advance through to loot generation
        _service.StartSession(mapSeed, abilityCards);
        _service.AdvanceSession(); // -> Exploring
        _service.AdvanceSession(); // -> InCombat
        _service.AdvanceSession(); // -> GeneratingLoot
        _service.AdvanceSession(); // -> SessionComplete (triggers loot generation)

        // Should have generated loot
        Assertions.AssertThat(_lastLootGenerated).IsNotNull();
        Assertions.AssertThat(_lastLootGenerated.Count).IsEqual(10); // Should generate 10 cards

        // Loot should be variations of the map seed
        foreach (var lootSignature in _lastLootGenerated)
        {
            Assertions.AssertThat(lootSignature).IsNotNull();
            
            // Each signature should have 8 elements with values between -1 and 1
            for (int i = 0; i < 8; i++)
            {
                Assertions.AssertThat(lootSignature[i]).IsBetween(-1.0f, 1.0f);
            }
        }
    }

    [TestCase]
    public void TestLootGenerationVariation()
    {
        var mapSeed = new CardSignature(new[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var abilityCards = new List<CardSignature> { new CardSignature() };
        
        // Generate loot twice to ensure variation
        _service.StartSession(mapSeed, abilityCards);
        _service.AdvanceSession(); // -> Exploring  
        _service.AdvanceSession(); // -> InCombat
        _service.AdvanceSession(); // -> GeneratingLoot
        _service.AdvanceSession(); // -> SessionComplete
        
        var firstLoot = new List<CardSignature>(_lastLootGenerated);
        
        // Reset and generate again
        _service.ResetSession();
        _service.StartSession(mapSeed, abilityCards);
        _service.AdvanceSession(); // -> Exploring
        _service.AdvanceSession(); // -> InCombat  
        _service.AdvanceSession(); // -> GeneratingLoot
        _service.AdvanceSession(); // -> SessionComplete
        
        var secondLoot = _lastLootGenerated;
        
        // Should have different loot (due to randomness)
        bool foundDifference = false;
        for (int i = 0; i < firstLoot.Count && i < secondLoot.Count; i++)
        {
            for (int j = 0; j < 8; j++)
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
    }

    [TestCase]
    public void TestAdvanceFromSessionCompleteDoesNothing()
    {
        var mapSeed = new CardSignature();
        var abilityCards = new List<CardSignature> { new CardSignature() };
        
        // Get to session complete
        _service.StartSession(mapSeed, abilityCards);
        _service.AdvanceSession(); // -> Exploring
        _service.AdvanceSession(); // -> InCombat
        _service.AdvanceSession(); // -> GeneratingLoot
        _service.AdvanceSession(); // -> SessionComplete
        
        int stateChangesBeforeExtra = _stateChangeCount;
        
        // Try to advance further
        _service.AdvanceSession();
        
        // Should remain in SessionComplete
        Assertions.AssertThat(_service.CurrentState).IsEqual(SessionState.SessionComplete);
        Assertions.AssertThat(_stateChangeCount).IsEqual(stateChangesBeforeExtra); // No new state changes
    }
}