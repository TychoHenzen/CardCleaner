using System.Collections.Generic;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Models.Effects;
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
}
