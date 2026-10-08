using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Models.Effects;
using CardCleaner.Scripts.Features.Card.Services;
using Godot;

namespace CardCleaner.Tests.Features.Card.Models.Effects;

/// <summary>
///     The seed that places a card's scratches comes from its signature alone, stays the same in every run, stays
///     below the shader's precision limit, and spreads different signatures over the whole seed range.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardEffectSeedTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TheAllZeroSignatureHasAPinnedSeed()
    {
        // Pinned so that a hash that varies between runs (string or HashCode hashing) fails here.
        AssertThat(CardEffectSeed.For(new CardSignature())).IsEqual(635f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TheEffectSeedDependsOnlyOnTheSignature()
    {
        var first = CardEffectSeed.For(new CardSignature { Solidum = 0.7f, Febris = -0.5f });
        var again = CardEffectSeed.For(new CardSignature { Solidum = 0.7f, Febris = -0.5f });
        var other = CardEffectSeed.For(new CardSignature { Solidum = 0.5f, Febris = -0.3f, Ordinem = 0.8f });

        AssertThat(again).IsEqual(first);
        AssertThat(first).IsEqual(8634f);
        AssertThat(other).IsEqual(8268f);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SignaturesOnACoarseGridStillGetDifferentSeeds()
    {
        // Hand-made signatures (the comparison grid, tests) use steps of 0.05; their seeds must not bunch up.
        var rng = new RandomNumberGenerator { Seed = 7 };
        var seeds = new HashSet<float>();
        for (var i = 0; i < 200; i++)
        {
            var elements = new float[8];
            for (var e = 0; e < elements.Length; e++)
                elements[e] = rng.RandiRange(-20, 20) * 0.05f;
            var seed = CardEffectSeed.For(new CardSignature(elements));

            AssertThat(seed).IsGreaterEqual(0f).IsLess(10000f);
            AssertThat(seed % 1f).IsEqual(0f);
            seeds.Add(seed);
        }

        // 200 seeds drawn evenly from 10000 values share about two; far more means the seed throws away entropy.
        AssertThat(seeds.Count).IsGreaterEqual(195);
    }

    [TestCase(new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f }, 1331286749777UL, 635f)]
    [TestCase(new[] { 0.7f, -0.5f, 0f, 0f, 0f, 0f, 0f, 0f }, 3640646618177UL, 8634f)]
    [TestCase(new[] { 0.5f, -0.3f, 0.8f, 0f, 0f, 0f, 0f, 0f }, 2994437780977UL, 8268f)]
    [TestCase(new[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f }, 4890876989777UL, 7309f)]
    [TestCase(new[] { -1f, -1f, -1f, -1f, -1f, -1f, -1f, -1f }, 18446741845406061393UL, 6728f)]
    [TestCase(new[] { 0.25f, -0.75f, 0.5f, -0.05f, 1f, -1f, 0.35f, 0.65f }, 2074682020927UL, 4053f)]
    [TestCategory("Unit")]
    public static void TheSeedsOfKnownSignaturesArePinned(float[] elements, ulong expectedHash, float expectedScratch)
    {
        // Pinned so that a change to the hash or to the seed mixing, which moves every card's scratches, fails here.
        var signature = new CardSignature(elements);
        AssertThat(SignatureCardHelper.ComputeSeed(signature)).IsEqual(expectedHash);
        AssertThat(CardEffectSeed.For(signature)).IsEqual(expectedScratch);
    }
}
