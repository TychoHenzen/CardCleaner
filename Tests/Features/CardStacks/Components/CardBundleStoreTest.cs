using System.Linq;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.CardStacks.Components;

/// <summary>
///     A deck or pile as an item store: it gives its last card in first, keeps only cards, grows by one on a put, and
///     is gone (and takes nothing more) once its last card is taken.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class CardBundleStoreTest
{
    private static StoredCard Card(int look) => new(null, look, null);

    private static CardBundle InTree(CardBundleKind kind, int count)
    {
        var bundle = CardBundle.Create(kind, Enumerable.Range(1, count).Select(Card));
        AddNode(bundle);
        return bundle;
    }

    [TestCase]
    public static void PeekAndTakeLastGiveTheLastCardIn()
    {
        var pile = InTree(CardBundleKind.Pile, 3);

        AssertThat(pile.Peek()).IsEqual(Card(3));
        AssertThat(pile.TakeLast()).IsEqual(Card(3));
        AssertThat(pile.Count).IsEqual(2);
        AssertThat(pile.Peek()).IsEqual(Card(2));
    }

    [TestCase]
    public static void APutAddsACardOnTopAndAContainerIsRefused()
    {
        var deck = InTree(CardBundleKind.Deck, 2);
        var booster = new StoredContainer("res://Scenes/Shop/Items/Booster.tscn", CardContainerKind.Booster, 1, null);

        AssertBool(deck.CanPut(booster)).IsFalse();
        AssertBool(deck.Put(booster)).IsFalse();
        AssertBool(deck.Put(Card(9))).IsTrue();

        AssertThat(deck.Count).IsEqual(3);
        AssertThat(deck.Peek()).IsEqual(Card(9));
    }

    [TestCase]
    public static void TakingTheLastCardRemovesTheBundleAndItTakesNothingMore()
    {
        var pile = InTree(CardBundleKind.Pile, 1);

        AssertThat(pile.TakeLast()).IsEqual(Card(1));

        AssertBool(pile.IsInsideTree()).IsFalse();
        AssertBool(pile.IsQueuedForDeletion()).IsTrue();
        AssertThat(pile.Peek()).IsNull();
        AssertThat(pile.TakeLast()).IsNull();
        AssertBool(pile.CanPut(Card(2))).IsFalse();
    }
}
