using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

/// <summary>
/// Generates the loot cards awarded when a session completes.
/// </summary>
internal static class LootGenerator
{
    private const int MinLootCount = 5;
    private const int MaxLootCount = 10;
    private const float SignatureSpread = 0.1f;

    /// <summary>
    /// Generates 5-10 cards that vary around the base map seed.
    /// </summary>
    internal static List<CardSignature> Generate(CardSignature baseSeed, RandomNumberGenerator rng)
    {
        var lootCount = rng.RandiRange(MinLootCount, MaxLootCount);
        var loot = new List<CardSignature>();

        for (var i = 0; i < lootCount; i++)
            loot.Add(GenerateSignature(baseSeed, rng));

        return loot;
    }

    private static CardSignature GenerateSignature(CardSignature baseSeed, RandomNumberGenerator rng)
    {
        var lootSignature = new CardSignature();

        for (var i = 0; i < 8; i++)
        {
            var variation = rng.Randfn(baseSeed[i], SignatureSpread);
            lootSignature[i] = Mathf.Clamp(variation, -1f, 1f);
        }

        return lootSignature;
    }
}
