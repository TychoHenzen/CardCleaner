using System.Linq;
using CardCleaner.Scripts.Features.CardStacks.Models;
using Godot;

namespace CardCleaner.Tests.Features.CardStacks.Models;

/// <summary>
///     A pile card's pose comes from the pile seed and its index alone, so a pile that grows or shrinks moves no card
///     it already showed, and the same seed always lays the same heap.
/// </summary>
[TestSuite]
public class PileLayoutTest
{
    private const int Seed = 4711;

    // The pose of a card is the same whether it is computed before or after any number of later cards.
    [TestCase]
    public static void AddingCardsNeverChangesAnEarlierCardsPose()
    {
        var before = Enumerable.Range(0, 64).Select(i => PileLayout.PoseOf(Seed, i)).ToArray();

        var later = Enumerable.Range(64, 2000).Select(i => PileLayout.PoseOf(Seed, i)).ToArray();
        var after = Enumerable.Range(0, 64).Select(i => PileLayout.PoseOf(Seed, i)).ToArray();

        AssertThat(later.Length).IsEqual(2000);
        AssertThat(after).IsEqual(before);
    }

    [TestCase]
    public static void TheSameSeedLaysTheSameHeapAndAnotherSeedADifferentOne()
    {
        var first = Enumerable.Range(0, 100).Select(i => PileLayout.PoseOf(Seed, i)).ToArray();
        var again = Enumerable.Range(0, 100).Select(i => PileLayout.PoseOf(Seed, i)).ToArray();
        var other = Enumerable.Range(0, 100).Select(i => PileLayout.PoseOf(Seed + 1, i)).ToArray();

        AssertThat(again).IsEqual(first);
        AssertThat(first.Zip(other).Count(pair => pair.First.Origin.IsEqualApprox(pair.Second.Origin)))
            .IsLess(5);
    }

    // Neighbouring cards draw independently, so they do not lie in a line or share a turn.
    [TestCase]
    public static void NeighbouringCardsLieApart()
    {
        var spots = Enumerable.Range(0, 200).Select(i => PileLayout.PoseOf(Seed, i).Origin).ToArray();

        var close = spots.Zip(spots.Skip(1)).Count(pair => pair.First.DistanceTo(pair.Second) < 0.01f);
        AssertThat(close).IsLess(5);
    }

    // Card i lies on the dome of a pile of i + 1 cards (the pile once it was in), a hair above it.
    [TestCase(30)]
    [TestCase(500)]
    [TestCase(1999)]
    public static void ACardLiesOnTheDomeOfThePileItCompleted(int index)
    {
        var radius = CardBundleRules.PileRadius(index + 1);
        var height = CardBundleRules.PileHeight(index + 1);

        foreach (var seed in Enumerable.Range(0, 20))
        {
            var spot = PileLayout.PoseOf(seed, index).Origin;
            var onDome = (spot.X * spot.X + spot.Z * spot.Z) / (radius * radius) + spot.Y * spot.Y / (height * height);
            AssertThat(onDome).IsBetween(0.98f, 1.25f);
        }
    }

    // Each card is turned so it lies face up on the mound.
    [TestCase]
    public static void EveryCardLiesFaceUp()
    {
        foreach (var i in Enumerable.Range(0, 300))
            AssertThat(PileLayout.PoseOf(Seed, i).Basis.Y.Y).IsGreater(0.5f);
    }

    [TestCase]
    public static void ThePileSeedComesFromTheCardsItStartsWith()
    {
        int?[] looks = [3, null, 17];

        AssertThat(PileLayout.SeedFor(looks)).IsEqual(PileLayout.SeedFor([3, null, 17]));
        AssertThat(PileLayout.SeedFor(looks)).IsNotEqual(PileLayout.SeedFor([3, null, 18]));
    }
}
