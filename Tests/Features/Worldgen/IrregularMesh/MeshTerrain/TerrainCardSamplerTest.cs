using System;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Worldgen;
using CardCleaner.Scripts.Features.Worldgen.IrregularMesh.WorldMap;
using Godot;

namespace CardCleaner.Tests.Features.Worldgen.IrregularMesh.MeshTerrain;

/// <summary>
/// Pins the signature sampler the mesh path builds from a map's seed cards: no cards give no sampler, and the
/// sampler is the card gradient seeded the way the generator always seeded it, including negative seeds.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class TerrainCardSamplerTest
{
    private static readonly Vector2I MapSize = new(20, 20);
    private static readonly Vector2I[] Positions = { new(0, 0), new(5, 7), new(19, 19) };

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyCardsGiveNoSampler()
    {
        AssertObject(TerrainGeneratorFactory.CardSampler(Array.Empty<CardSignature>(), 4242)).IsNull();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SamplerMatchesAGradientSeededTheSameWay()
    {
        AssertSamplerMatches(4242);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NegativeSeedKeepsTheUlongCast()
    {
        AssertSamplerMatches(-5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NullCardsThrow()
    {
        Assertions.AssertThrown(() => TerrainGeneratorFactory.CardSampler(null!, 4242))
            .IsInstanceOf<ArgumentNullException>();
    }

    private static void AssertSamplerMatches(int seed)
    {
        var cards = MeshMapGeneratorFixture.TerrainCards();
        var sampler = TerrainGeneratorFactory.CardSampler(cards, seed)!;
        var reference = new CardBasedGradient(cards, RandomSources.Create((ulong)seed));
        foreach (var position in Positions)
        {
            var actual = sampler(position, MapSize);
            AssertFloat(actual.DistanceTo(reference.GetSignatureAt(position, MapSize))).IsEqual(0f);
        }
    }
}
