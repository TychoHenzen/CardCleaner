using System.Collections.Generic;
using System.Threading.Tasks;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session;
using Godot;


namespace CardCleaner.Tests.Features.Deckbuilder.Services.GameSessionScenarios;

/// <summary>
///     GameSessionService deterministic seeding scenarios split out of GameSessionServiceTest.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class GameSessionSeedTest : GameSessionServiceTestBase
{
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

        var seed1 = MapSeeding.ComputeSeedFromCards(cards1);
        var seed2 = MapSeeding.ComputeSeedFromCards(cards2);

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

        var seed1 = MapSeeding.ComputeSeedFromCards(cards1);
        var seed2 = MapSeeding.ComputeSeedFromCards(cards2);

        Assertions.AssertThat(seed1).IsNotEqual(seed2);
    }

    [TestCase]
    public static void ComputeSeedFromCards_OrderMatters()
    {
        // [A, B] should produce different seed than [B, A]
        var cardA = new CardSignature(new[] { 0.5f, -0.3f, 0.8f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });
        var cardB = new CardSignature(new[] { 1.0f, 0.0f, -0.5f, 0.25f, 0.0f, 0.0f, 0.0f, 0.0f });

        var cardsAB = new List<CardSignature> { cardA, cardB };
        var cardsBA = new List<CardSignature> { cardB, cardA };

        var seedAB = MapSeeding.ComputeSeedFromCards(cardsAB);
        var seedBA = MapSeeding.ComputeSeedFromCards(cardsBA);

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
