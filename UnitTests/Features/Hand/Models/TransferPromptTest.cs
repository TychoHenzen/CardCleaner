using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Hand.Models;

/// <summary>The click prompt for each transfer decision, in today's wording where the move is unchanged.</summary>
[TestSuite]
public class TransferPromptTest
{
    private static readonly StoredItem Card = new StoredCard(null, 1, null);

    private static readonly StoredItem Booster =
        new StoredContainer("res://Scenes/Shop/Items/Booster.tscn", CardContainerKind.Booster, 1, null);

    private static readonly StoredItem BoosterBox =
        new StoredContainer("res://Scenes/Shop/Items/Pack.tscn", CardContainerKind.Pack, 1, null);

    [TestCase]
    public static void ADecisionThatMovesNothingHasNoPrompt()
    {
        foreach (var kind in new[] { StoreKind.Pile, StoreKind.Deck, StoreKind.ShelfLevel, StoreKind.OpenBox })
        {
            AssertThat(TransferPrompt.For(TransferDecision.None, Card, kind, 5)).IsNull();
            AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.TakeMany, 0), Card, kind, 5)).IsNull();
        }
    }

    [TestCase]
    public static void ADecisionWithNoItemHasNoPrompt()
    {
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.Put, 1), null, StoreKind.OpenBox, 0)).IsNull();
    }

    [TestCase]
    public static void TheMovesOnACardboardBoxItselfNeedNoItem()
    {
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.PickUp, 1), null, StoreKind.OpenBox, 3))
            .IsEqual("Pick up cardboard box");
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.Discard, 1), null, StoreKind.OpenBox, 0))
            .IsEqual("Throw the box away");
    }

    [TestCase]
    public static void ASetDownNamesTheItemThatGoesOnTheFloor()
    {
        var setDown = new TransferDecision(TransferMove.SetDown, 1);

        AssertThat(TransferPrompt.For(setDown, Card, StoreKind.OpenBox, 4)).IsEqual("Put the card on the floor");
        AssertThat(TransferPrompt.For(setDown, BoosterBox, StoreKind.OpenBox, 4))
            .IsEqual("Put the booster box on the floor");
    }

    [TestCase]
    public static void APutNamesTheItemItStocks()
    {
        var put = new TransferDecision(TransferMove.Put, 1);

        AssertThat(TransferPrompt.For(put, Card, StoreKind.ShelfLevel, 0)).IsEqual("Put the card here");
        AssertThat(TransferPrompt.For(put, Booster, StoreKind.ShelfLevel, 2)).IsEqual("Put the booster pack here");
        AssertThat(TransferPrompt.For(put, BoosterBox, StoreKind.OpenBox, 0)).IsEqual("Put the booster box here");
    }

    [TestCase]
    public static void TakingOneOffAShelfLevelPicksTheItemUp()
    {
        var one = new TransferDecision(TransferMove.TakeOne, 1);

        AssertThat(TransferPrompt.For(one, Card, StoreKind.ShelfLevel, 3)).IsEqual("Pick up card");
        AssertThat(TransferPrompt.For(one, Booster, StoreKind.ShelfLevel, 3)).IsEqual("Pick up booster pack");
    }

    [TestCase]
    public static void TakingFromAPileCountsTheCards()
    {
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.TakeOne, 1), Card, StoreKind.Pile, 20))
            .IsEqual("Take 1 card from the pile (20 in it)");
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.TakeMany, 3), Card, StoreKind.Pile, 5))
            .IsEqual("Take 3 cards from the pile (5 in it)");
    }

    [TestCase]
    public static void TakingADeckWholeUnstrapsIt()
    {
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.TakeWhole, 3), Card, StoreKind.Deck, 3))
            .IsEqual("Unstrap the deck (3 cards)");
    }

    [TestCase]
    public static void AShiftTakeFromABoxNamesHowManyAndWhat()
    {
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.TakeMany, 100), Card, StoreKind.OpenBox, 300))
            .IsEqual("Take 100 cards from the box (300 in it)");
        AssertThat(TransferPrompt.For(new TransferDecision(TransferMove.TakeMany, 8), BoosterBox, StoreKind.OpenBox, 8))
            .IsEqual("Take 8 booster boxes from the box (8 in it)");
    }
}
