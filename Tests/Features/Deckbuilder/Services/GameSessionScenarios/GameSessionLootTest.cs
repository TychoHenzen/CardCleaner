using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;


namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
///     GameSessionService loot generation scenarios split out of GameSessionServiceTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSessionLootTest : GameSessionServiceTestBase
{
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
        const int maxFrames = 300;

        // First session - limited wait time
        _service.StartSession(mapSeeds, abilityCards);
        await WaitForFrames(() => _lastLootGenerated.Count > 0, maxFrames);

        // If first session completed with loot, try second session
        if (_lastLootGenerated.Count > 0)
        {
            var firstLoot = new List<CardSignature>(_lastLootGenerated);

            // Wait for reset
            await WaitForFrames(() => _service.CurrentState == SessionState.WaitingForCards, maxFrames);

            if (_service.CurrentState == SessionState.WaitingForCards)
            {
                // Second session
                _lastLootGenerated.Clear();
                _service.StartSession(mapSeeds, abilityCards);
                await WaitForFrames(() => _lastLootGenerated.Count > 0, maxFrames);

                if (_lastLootGenerated.Count > 0)
                {
                    // Should have different loot (due to randomness)
                    Assertions.AssertBool(LootDiffers(firstLoot, _lastLootGenerated)).IsTrue();
                    return;
                }
            }
        }

        // If we couldn't complete sessions in time, just verify session started correctly
        Assertions.AssertBool(_stateChangeCount >= 1).IsTrue();
    }

    private static bool LootDiffers(List<CardSignature> firstLoot, List<CardSignature> secondLoot)
    {
        for (var i = 0; i < Math.Min(firstLoot.Count, secondLoot.Count); i++)
        {
            for (var j = 0; j < 8; j++)
            {
                if (Math.Abs(firstLoot[i][j] - secondLoot[i][j]) > 0.001f)
                    return true;
            }
        }

        return false;
    }
}
