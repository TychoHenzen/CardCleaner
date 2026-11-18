using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services.Session;

namespace CardCleaner.Tests.Features.Deckbuilder.Services.Session;

/// <summary>
/// The loot a won session awards: 5 to 10 cards, each element drawn around the map seed's and clamped to [-1, 1].
/// The session's own wiring (a won fight raises loot, a lost one none) is pinned by GameSessionFlowTest.
/// </summary>
[TestSuite]
public class LootGeneratorTest
{
    private const int Seeds = 200;

    // Every element at least 0.4 from the range's ends, four spreads, so clamping does not bend the draws measured.
    private static readonly CardSignature MapSeed = new(new[] { 0.5f, -0.3f, 0.6f, -0.1f, 0.2f, -0.6f, 0.4f, -0.4f });

    [TestCase]
    public static void EveryLootCountFromFiveToTenOccursAndNoOther()
    {
        var counts = Enumerable.Range(0, Seeds)
            .Select(seed => LootGenerator.Generate(MapSeed, new Pcg32Random((ulong)seed)).Count)
            .Distinct()
            .Order()
            .ToArray();

        AssertThat(counts).IsEqual(new[] { 5, 6, 7, 8, 9, 10 });
    }

    [TestCase]
    public static void ElementsAtTheEdgeOfTheRangeStayWithinIt()
    {
        var edge = new CardSignature(new[] { 1f, -1f, 1f, -1f, 1f, -1f, 1f, -1f });
        var elements = Draw(edge).SelectMany(card => card.Elements).ToList();

        AssertThat(elements.Min()).IsGreaterEqual(-1f);
        AssertThat(elements.Max()).IsLessEqual(1f);
        // Half the draws of an edge element fall outside the range, so clamping puts many of them exactly on it.
        AssertInt(elements.Count(e => Math.Abs(e) == 1f)).IsGreater(elements.Count / 4);
    }

    [TestCase]
    public static void EachElementVariesNarrowlyAroundTheMapSeed()
    {
        var loot = Draw(MapSeed);

        for (var i = 0; i < 8; i++)
        {
            var element = i;
            var values = loot.Select(card => (double)card[element]).ToList();
            var mean = values.Average();
            var spread = Math.Sqrt(values.Average(v => (v - mean) * (v - mean)));

            // The draws are normal with a 0.1 spread around the seed's element. Over about 1500 cards the mean lies
            // within 0.02 of it and the measured spread within 0.08 to 0.12.
            AssertThat(Math.Abs(mean - MapSeed[element])).IsLess(0.02);
            AssertThat(spread).IsBetween(0.08, 0.12);
        }
    }

    [TestCase]
    public static void TheSameRandomStateGivesTheSameLootAndAnotherGivesOther()
    {
        var first = Flatten(LootGenerator.Generate(MapSeed, new Pcg32Random(7UL)));
        var again = Flatten(LootGenerator.Generate(MapSeed, new Pcg32Random(7UL)));
        var other = Flatten(LootGenerator.Generate(MapSeed, new Pcg32Random(8UL)));

        AssertThat(again).IsEqual(first);
        AssertBool(other.SequenceEqual(first)).IsFalse();
    }

    // The loot of every seed in turn, all in one list.
    private static List<CardSignature> Draw(CardSignature mapSeed) =>
        Enumerable.Range(0, Seeds)
            .SelectMany(seed => LootGenerator.Generate(mapSeed, new Pcg32Random((ulong)seed)))
            .ToList();

    private static float[] Flatten(List<CardSignature> loot) => loot.SelectMany(card => card.Elements).ToArray();
}
