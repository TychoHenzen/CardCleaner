using CardCleaner.Scripts.Features.CardStacks.Models;
using Godot;

namespace CardCleaner.Tests.Features.CardStacks.Models;

[TestSuite]
public class CardBundleRulesTest
{
    [TestCase]
    public static void ADeckIsOneCardWideAndAsTallAsItsCards()
    {
        var size = CardBundleRules.DeckSize(40);

        AssertThat(size.X).IsEqualApprox(CardBundleRules.CardSize.X, 1e-6f);
        AssertThat(size.Z).IsEqualApprox(CardBundleRules.CardSize.Z, 1e-6f);
        AssertThat(size.Y).IsEqualApprox(40 * CardBundleRules.CardSize.Y, 1e-6f);
    }

    // A pile holds its cards' volume, so eight times the cards make it twice as wide (once it is past its smallest).
    [TestCase]
    public static void APileGrowsWithTheCubeRootOfItsCount()
    {
        var radius = CardBundleRules.PileRadius(1000);

        AssertThat(CardBundleRules.PileRadius(8000)).IsEqualApprox(2f * radius, 1e-4f);
        AssertThat(CardBundleRules.PileHeight(1000)).IsLess(radius);
        AssertThat(radius).IsGreater(0.3f);
    }

    [TestCase]
    public static void ASmallPileIsNeverNarrowerThanACard()
    {
        AssertThat(CardBundleRules.PileRadius(101) * 2f).IsGreaterEqual(CardBundleRules.CardSize.X);
        AssertThat(CardBundleRules.PileRadius(0)).IsEqual(CardBundleRules.PileRadius(1));
    }
}
