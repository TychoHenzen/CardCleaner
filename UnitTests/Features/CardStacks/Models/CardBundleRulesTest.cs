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

    [TestCase]
    public static void ADeckComesOutWholeOrNotAtAll()
    {
        AssertThat(CardBundleRules.TakeCount(CardBundleKind.Deck, 30, 30)).IsEqual(30);
        AssertThat(CardBundleRules.TakeCount(CardBundleKind.Deck, 30, 29)).IsEqual(0);
    }

    [TestCase]
    public static void APileGivesAsManyCardsAsFit()
    {
        AssertThat(CardBundleRules.TakeCount(CardBundleKind.Pile, 300, 100)).IsEqual(100);
        AssertThat(CardBundleRules.TakeCount(CardBundleKind.Pile, 30, 100)).IsEqual(30);
        AssertThat(CardBundleRules.TakeCount(CardBundleKind.Pile, 30, 0)).IsEqual(0);
    }

    [TestCase]
    public static void ThePromptSaysWhatAClickTakes()
    {
        AssertString(CardBundleRules.Prompt(CardBundleKind.Deck, 12, 12)).IsEqual("Unstrap the deck (12 cards)");
        AssertString(CardBundleRules.Prompt(CardBundleKind.Pile, 250, 100))
            .IsEqual("Take 100 cards from the pile (250 in it)");
        AssertString(CardBundleRules.Prompt(CardBundleKind.Pile, 250, 1)).IsEqual("Take 1 card from the pile (250 in it)");
    }
}
