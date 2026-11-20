using System.Linq;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Hand.Models;

[TestSuite]
public class HandHintsTest
{
    private static string[] Lines(HandHintState state) =>
        HandHints.For(state).Select(hint => $"{hint.Key}: {hint.Action}").ToArray();

    [TestCase]
    public static void AnEmptyHandHasNoHints()
    {
        AssertThat(HandHints.For(new HandHintState(null, 0, 0)).Count).IsEqual(0);
    }

    [TestCase]
    public static void ACardInFrontShowsTheDropKeysTheWheelAndTheStrapKey()
    {
        AssertThat(Lines(new HandHintState(null, 3, 3))).IsEqual(new[]
        {
            "RMB: Hold to aim, release to set down (3 cards in hand)",
            "Q: Put the card down",
            "X: Drop the front card",
            "Wheel: Bring the next item to the front",
            "V: Strap the cards into a deck",
        });
    }

    [TestCase]
    public static void OneCardHasNoWheelAndIsNotStrapped()
    {
        AssertThat(HandHints.For(new HandHintState(null, 1, 1)).Select(hint => hint.Key).ToArray())
            .IsEqual(new[] { "RMB", "Q", "X" });
    }

    [TestCase]
    public static void AimingADropShowsOnlyTheRelease()
    {
        var hints = HandHints.For(new HandHintState(null, 2, 1, AimingDrop: true));

        AssertThat(hints.Count).IsEqual(1);
        AssertThat(hints[0].Action).IsEqual("Set the card down");
    }

    [TestCase]
    public static void AShutBoxOpensWithTheRightButtonAndQPutsItDown()
    {
        AssertThat(Lines(new HandHintState(CardContainerKind.Box, 1, 0))).IsEqual(new[]
        {
            "RMB: Open the cardboard box",
            "Q: Put it down",
        });
    }

    [TestCase]
    public static void AnOpenCardboardBoxSetsOneItemDownWithTheLeftButton()
    {
        AssertThat(Lines(new HandHintState(CardContainerKind.Box, 1, 0, LidOpen: true, ItemsInside: 3))).IsEqual(new[]
        {
            "RMB: Close the cardboard box",
            "Q: Put it down",
            "LMB: Put one on the floor",
        });
    }

    [TestCase]
    public static void AnEmptyOpenCardboardBoxGoesInATrashCan()
    {
        var hints = HandHints.For(new HandHintState(CardContainerKind.Box, 1, 0, LidOpen: true));

        AssertThat(hints[^1].Action).IsEqual("Throw it in a trash can");
    }

    [TestCase]
    public static void ABoosterBoxEmptiesIntoTheHandWithTheRightButton()
    {
        AssertThat(Lines(new HandHintState(CardContainerKind.Pack, 1, 0))).IsEqual(new[]
        {
            "RMB: Empty the booster box into your hand",
            "Q: Put it down",
        });
    }

    [TestCase]
    public static void ABoosterInFrontOfCardsOpensAndTheCardsStillStrap()
    {
        AssertThat(Lines(new HandHintState(CardContainerKind.Booster, 4, 3))).IsEqual(new[]
        {
            "RMB: Open the booster pack",
            "Q: Put it down",
            "Wheel: Bring the next item to the front",
            "V: Strap the cards into a deck",
        });
    }

    // A booster pack opens into the hand, so it waits while the hand has no room for its eight cards.
    [TestCase]
    public static void ABoosterPackDoesNotOpenIntoAFullHand()
    {
        var hints = HandHints.For(new HandHintState(CardContainerKind.Booster, 94, 93, BoosterFits: false));

        AssertThat(hints[0].Action).IsEqual("No room in your hand for its cards");
    }

    [TestCase]
    public static void ShiftClickIsNamedWhenItWouldTakeMany()
    {
        AssertThat(Lines(new HandHintState(null, 0, 0, ShiftTakesMany: true)))
            .IsEqual(new[] { "Shift+LMB: Take as many as fit" });
        AssertThat(HandHints.For(new HandHintState(null, 1, 1, ShiftTakesMany: true))[^1].Key).IsEqual("Shift+LMB");
    }

    [TestCase]
    public static void EveryKindHasTheNameThePlayerReads()
    {
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Box)).IsEqual("cardboard box");
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Pack)).IsEqual("booster box");
        AssertThat(CardContainerKindText.NameOf(CardContainerKind.Booster)).IsEqual("booster pack");
    }
}
