using System.Linq;
using CardCleaner.Scripts.Features.Hand.Models;

namespace CardCleaner.Tests.Features.Hand.Models;

/// <summary>
///     The hand's order and capacity rules over plain strings: a name starting with "box" stands for a booster box or a
///     cardboard box (exclusive), anything else for a card or a booster pack (one place).
/// </summary>
[TestSuite]
public class HandItemsTest
{
    private static HandItems<string> NewHand(params string[] items)
    {
        var hand = new HandItems<string>(item => item.StartsWith("box"));
        foreach (var item in items)
            AssertThat(hand.Add(item)).IsTrue();
        return hand;
    }

    private static string[] Cards(int count) => Enumerable.Range(0, count).Select(i => $"card{i}").ToArray();

    [TestCase]
    public static void AnEmptyHandHasNoFrontAndItsWholeRoom()
    {
        var hand = NewHand();

        AssertThat(hand.FrontIndex).IsEqual(-1);
        AssertThat(hand.Front).IsNull();
        AssertThat(hand.Room).IsEqual(HandItems<string>.MostPlaces);
    }

    [TestCase]
    public static void ItemsKeepTheOrderTheyWereTakenIn()
    {
        var hand = NewHand("card0", "booster", "card1");

        AssertThat(hand.Items.ToArray()).IsEqual(new[] { "card0", "booster", "card1" });
        AssertThat(hand.Front).IsEqual("card0");
    }

    [TestCase]
    public static void TheHandHoldsAtMostOneHundredPlaces()
    {
        var hand = NewHand(Cards(HandItems<string>.MostPlaces - 1));
        AssertThat(hand.Room).IsEqual(1);
        AssertThat(hand.Add("booster")).IsTrue();

        AssertThat(hand.Room).IsEqual(0);
        AssertThat(hand.CanAdd("card100")).IsFalse();
        AssertThat(hand.Add("card100")).IsFalse();
        AssertThat(hand.Count).IsEqual(HandItems<string>.MostPlaces);
    }

    [TestCase]
    public static void ABoxIsTakenOnlyIntoAnEmptyHand()
    {
        var holding = NewHand("card0");

        AssertThat(holding.CanAdd("box")).IsFalse();
        AssertThat(holding.Add("box")).IsFalse();
        AssertThat(NewHand().CanAdd("box")).IsTrue();
    }

    [TestCase]
    public static void NothingJoinsAHeldBox()
    {
        var hand = NewHand("box");

        AssertThat(hand.HoldsExclusive).IsTrue();
        AssertThat(hand.Room).IsEqual(0);
        AssertThat(hand.CanAdd("card0")).IsFalse();
        AssertThat(hand.CanAdd("box2")).IsFalse();
    }

    [TestCase]
    public static void ScrollingUpMakesTheNextItemTheFront()
    {
        var hand = NewHand("card0", "card1", "card2");

        hand.Rotate(+1);

        AssertThat(hand.Front).IsEqual("card1");
    }

    [TestCase]
    public static void ScrollingWrapsAtEitherEnd()
    {
        var hand = NewHand("card0", "card1", "card2");

        hand.Rotate(-1);
        AssertThat(hand.Front).IsEqual("card2");

        hand.Rotate(+1);
        AssertThat(hand.Front).IsEqual("card0");
    }

    [TestCase]
    public static void ScrollingAnEmptyOrOneItemHandChangesNothing()
    {
        var empty = NewHand();
        empty.Rotate(+1);
        AssertThat(empty.FrontIndex).IsEqual(-1);

        var one = NewHand("card0");
        one.Rotate(-1);
        AssertThat(one.FrontIndex).IsEqual(0);
    }

    [TestCase]
    public static void ATakenItemLeavesTheFrontWhereItWas()
    {
        var hand = NewHand("card0", "card1");
        hand.Rotate(+1);

        hand.Add("booster");

        AssertThat(hand.Front).IsEqual("card1");
    }

    [TestCase]
    public static void RemovingTheFrontMakesTheNextItemTheFront()
    {
        var hand = NewHand("card0", "card1", "card2");

        hand.Remove("card0");

        AssertThat(hand.Front).IsEqual("card1");
    }

    [TestCase]
    public static void RemovingTheLastItemInFrontWrapsTheFrontToTheFirst()
    {
        var hand = NewHand("card0", "card1", "card2");
        hand.Rotate(-1);

        hand.Remove("card2");

        AssertThat(hand.FrontIndex).IsEqual(0);
        AssertThat(hand.Front).IsEqual("card0");
    }

    [TestCase]
    public static void RemovingAnItemBeforeTheFrontKeepsTheSameFrontItem()
    {
        var hand = NewHand("card0", "card1", "card2");
        hand.Rotate(+2);

        hand.Remove("card0");

        AssertThat(hand.Front).IsEqual("card2");
        AssertThat(hand.FrontIndex).IsEqual(1);
    }

    [TestCase]
    public static void RemovingAnItemAfterTheFrontKeepsTheFront()
    {
        var hand = NewHand("card0", "card1", "card2");

        hand.Remove("card2");

        AssertThat(hand.Front).IsEqual("card0");
    }

    [TestCase]
    public static void RemovingTheOnlyItemLeavesNoFront()
    {
        var hand = NewHand("box");

        AssertThat(hand.Remove("box")).IsTrue();

        AssertThat(hand.FrontIndex).IsEqual(-1);
        AssertThat(hand.Count).IsEqual(0);
        AssertThat(hand.Remove("box")).IsFalse();
    }

    // The right button opens a booster in front only while the hand has room for its 8 cards once it has left.
    [TestCase]
    public static void ABoosterInFrontOfFiftyItemsHasRoomForItsCards()
    {
        var hand = NewHand(["booster", .. Cards(49)]);

        AssertThat(hand.RoomOnceFrontLeaves).IsGreaterEqual(8);
        hand.Remove("booster");
        foreach (var card in Enumerable.Range(0, 8).Select(i => $"new{i}"))
            AssertThat(hand.Add(card)).IsTrue();
        AssertThat(hand.Count).IsEqual(57);
    }

    [TestCase]
    public static void ABoosterInFrontOfNinetyFourItemsHasNoRoomForItsCards()
    {
        AssertThat(NewHand(["booster", .. Cards(93)]).RoomOnceFrontLeaves).IsEqual(7);
        AssertThat(NewHand(["booster", .. Cards(92)]).RoomOnceFrontLeaves).IsEqual(8);
    }

    [TestCase]
    public static void ABoxInFrontLeavesTheWholeHandOnceItHasLeft()
    {
        // A booster box empties its 8 booster packs into the hand it alone held.
        AssertThat(NewHand("box").RoomOnceFrontLeaves).IsEqual(HandItems<string>.MostPlaces);
    }
}
