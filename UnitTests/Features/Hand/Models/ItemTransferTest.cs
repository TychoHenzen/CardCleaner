using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Tests.Features.Hand.Models;

/// <summary>
///     The one transfer rule over fake list stores: the aimed part decides between put and take, a take needs room in
///     the hand for the next item, Shift takes up to the room and at most 100, and piles and decks have no free space.
/// </summary>
[TestSuite]
public class ItemTransferTest
{
    private const int Lots = 1000;

    private static StoredItem Card(int look) => new StoredCard(null, look, null);

    private static StoredItem Booster(ulong seed) =>
        new StoredContainer("res://Scenes/Shop/Items/Booster.tscn", CardContainerKind.Booster, seed, null);

    private static StoredItem BoosterBox(ulong seed) =>
        new StoredContainer("res://Scenes/Shop/Items/Pack.tscn", CardContainerKind.Pack, seed, null);

    private static StoredItem[] Cards(int count) => Enumerable.Range(0, count).Select(Card).ToArray();

    private static bool IsBooster(StoredItem item) => item is StoredContainer { Kind: CardContainerKind.Booster };

    // A hand that holds cards and booster packs, but no booster box.
    private static ListStore Hand(params StoredItem[] items) =>
        new(item => item is StoredCard || IsBooster(item), Lots, items);

    private static ListStore Store(params StoredItem[] items) => new(_ => true, Lots, items);

    [TestCase]
    public static void AClickOnFreeSpacePutsTheFrontItemWhenTheStoreAcceptsIt()
    {
        var hand = Hand(Booster(1));
        var level = new ListStore(IsBooster, 4);

        var decision = ItemTransfer.Decide(hand, 99, level, StoreKind.ShelfLevel, AimPart.Space, shift: false);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.Put, 1));
        AssertThat(ItemTransfer.Apply(decision, hand, level)).IsEqual(1);
        AssertThat(hand.Count).IsEqual(0);
        AssertThat(level.Peek()).IsEqual(Booster(1));
    }

    [TestCase]
    public static void AClickOnFreeSpaceThatRefusesTheFrontItemMovesNothing()
    {
        var hand = Hand(Card(1));
        var level = new ListStore(IsBooster, 4);

        var decision = ItemTransfer.Decide(hand, 99, level, StoreKind.ShelfLevel, AimPart.Space, shift: false);

        AssertThat(decision).IsEqual(TransferDecision.None);
        AssertThat(ItemTransfer.Apply(decision, hand, level)).IsEqual(0);
        AssertThat(hand.Count).IsEqual(1);
    }

    [TestCase]
    public static void AClickOnFreeSpaceWithAnEmptyHandMovesNothing()
    {
        var decision = ItemTransfer.Decide(Hand(), 100, Store(), StoreKind.ShelfLevel, AimPart.Space, shift: false);

        AssertThat(decision).IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void AnEmptyHandPicksAnOpenBoxInTheWorldUpWhole()
    {
        var box = Store(Card(1));

        var decision = ItemTransfer.Decide(Hand(), 100, box, StoreKind.OpenBox, AimPart.Space, shift: false);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.PickUp, 1));
        // Picking the box up is a world move: no record leaves it.
        AssertThat(ItemTransfer.Apply(decision, Hand(), box)).IsEqual(0);
        AssertThat(box.Count).IsEqual(1);
    }

    [TestCase]
    public static void AHandHoldingACardPutsItIntoAnOpenBoxInTheWorld()
    {
        var hand = Hand(Card(1));
        var box = Store();

        var decision = ItemTransfer.Decide(hand, 99, box, StoreKind.OpenBox, AimPart.Space, shift: false);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.Put, 1));
        AssertThat(ItemTransfer.Apply(decision, hand, box)).IsEqual(1);
        AssertThat(box.Peek()).IsEqual(Card(1));
    }

    [TestCase]
    public static void AShiftClickOnAnOpenBoxInTheWorldTakesManyEvenWithAnEmptyHand()
    {
        var hand = Hand();
        var box = Store(Cards(5));

        var decision = ItemTransfer.Decide(hand, 100, box, StoreKind.OpenBox, AimPart.Space, shift: true);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.TakeMany, 5));
        AssertThat(ItemTransfer.Apply(decision, hand, box)).IsEqual(5);
    }

    [TestCase]
    public static void AHeldOpenBoxStocksItsLastItemTheStoreAcceptsAndNeverItself()
    {
        var box = new PickingStore(BoosterBox(1), BoosterBox(2), Card(3));
        var rackLevel = new ListStore(item => item is StoredContainer { Kind: CardContainerKind.Pack }, 2);

        var decision = ItemTransfer.Decide(box, 0, rackLevel, StoreKind.ShelfLevel, AimPart.Space, shift: false);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.Put, 1));
        AssertThat(ItemTransfer.Apply(decision, box, rackLevel)).IsEqual(1);
        AssertThat(rackLevel.Peek()).IsEqual(BoosterBox(2));
        AssertThat(box.Items).IsEqual(new[] { BoosterBox(1), Card(3) });
    }

    [TestCase]
    public static void AHeldOpenBoxWithNothingTheStoreAcceptsPutsNothing()
    {
        var box = new PickingStore(Card(1), Card(2));
        var rackLevel = new ListStore(item => item is StoredContainer, 2);

        AssertThat(ItemTransfer.Decide(box, 0, rackLevel, StoreKind.ShelfLevel, AimPart.Space, shift: false))
            .IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void AHeldOpenBoxPutsNothingOnAFullLevelOrOnAPileOrDeck()
    {
        var box = new PickingStore(Card(1));
        var fullLevel = new ListStore(_ => true, 1, [Card(9)]);

        AssertThat(ItemTransfer.Decide(box, 0, fullLevel, StoreKind.ShelfLevel, AimPart.Space, shift: false))
            .IsEqual(TransferDecision.None);
        AssertThat(ItemTransfer.Decide(box, 0, Store(Card(8)), StoreKind.Pile, AimPart.Stock, shift: false))
            .IsEqual(TransferDecision.None);
        AssertThat(ItemTransfer.Decide(box, 0, Store(Card(8)), StoreKind.Deck, AimPart.Stock, shift: false))
            .IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void AHeldOpenBoxPutsIntoAnotherOpenBox()
    {
        var box = new PickingStore(Card(1));
        var other = Store();

        var decision = ItemTransfer.Decide(box, 0, other, StoreKind.OpenBox, AimPart.Space, shift: false);

        AssertThat(ItemTransfer.Apply(decision, box, other)).IsEqual(1);
        AssertThat(other.Peek()).IsEqual(Card(1));
        AssertThat(box.Count).IsEqual(0);
    }

    [TestCase]
    public static void ACardboardBoxRecordMovesOnAndOffALevelThroughTheOneRule()
    {
        var shutBox = new StoredBox("res://Scenes/Shop/Items/CardboardBox.tscn", 5UL, null, false);
        var hand = new ListStore(item => item is StoredBox, 1, [shutBox]);
        var rack = new ListStore(_ => true, 2);

        var put = ItemTransfer.Decide(hand, 0, rack, StoreKind.ShelfLevel, AimPart.Space, shift: false);

        AssertThat(put).IsEqual(new TransferDecision(TransferMove.Put, 1));
        AssertThat(ItemTransfer.Apply(put, hand, rack)).IsEqual(1);
        AssertThat(rack.Peek()).IsEqual(shutBox);

        var emptyHand = new ListStore(item => item is StoredBox, 1);
        var take = ItemTransfer.Decide(emptyHand, 1, rack, StoreKind.ShelfLevel, AimPart.Stock, shift: false);

        AssertThat(take).IsEqual(new TransferDecision(TransferMove.TakeOne, 1));
        AssertThat(ItemTransfer.Apply(take, emptyHand, rack)).IsEqual(1);
        AssertThat(emptyHand.Peek()).IsEqual(shutBox);
    }

    [TestCase]
    public static void AHeldOpenBoxPutsOnTheStockPartOfALevelAndShiftChangesNothing()
    {
        var box = new PickingStore(Card(1), Card(2));
        var level = new ListStore(_ => true, 4, [Card(9)]);

        AssertThat(ItemTransfer.Decide(box, 0, level, StoreKind.ShelfLevel, AimPart.Stock, shift: false))
            .IsEqual(new TransferDecision(TransferMove.Put, 1));
        AssertThat(ItemTransfer.Decide(box, 0, level, StoreKind.ShelfLevel, AimPart.Stock, shift: true))
            .IsEqual(new TransferDecision(TransferMove.Put, 1));
        AssertThat(ItemTransfer.NextToPut(box, level)).IsEqual(Card(2));
    }

    [TestCase]
    public static void AClickOnNothingWithAHeldOpenBoxSetsItsLastItemDownWhileItHoldsAny()
    {
        AssertThat(ItemTransfer.DecideFromHeldBoxOntoNothing(new PickingStore(Card(1))))
            .IsEqual(new TransferDecision(TransferMove.SetDown, 1));
        AssertThat(ItemTransfer.DecideFromHeldBoxOntoNothing(new PickingStore())).IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void TheTrashCanTakesOnlyAnEmptyCardboardBox()
    {
        AssertThat(ItemTransfer.DecideTrash(new PickingStore())).IsEqual(new TransferDecision(TransferMove.Discard, 1));
        AssertThat(ItemTransfer.DecideTrash(new PickingStore(Card(1)))).IsEqual(TransferDecision.None);
        // The front item is no cardboard box.
        AssertThat(ItemTransfer.DecideTrash(null)).IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void AnItemTheStoreRefusesAfterAllGoesBackIntoTheHeldBox()
    {
        var box = new PickingStore(Card(1));
        var level = new RefusesPuts(Store());

        var moved = ItemTransfer.Apply(new TransferDecision(TransferMove.Put, 1), box, level);

        AssertThat(moved).IsEqual(0);
        AssertThat(box.Items).IsEqual(new[] { Card(1) });
    }

    [TestCase]
    public static void TakePolicyIsOneForEveryStoreButADeck()
    {
        AssertThat(ItemTransfer.PolicyOf(StoreKind.Pile)).IsEqual(TakePolicy.One);
        AssertThat(ItemTransfer.PolicyOf(StoreKind.OpenBox)).IsEqual(TakePolicy.One);
        AssertThat(ItemTransfer.PolicyOf(StoreKind.ShelfLevel)).IsEqual(TakePolicy.One);
        AssertThat(ItemTransfer.PolicyOf(StoreKind.Deck)).IsEqual(TakePolicy.WholeIfFits);
    }

    [TestCase]
    public static void AClickOnStockTakesOneWhileACardIsInFront()
    {
        var hand = Hand(Card(1));
        var level = Store(Card(2), Card(3));

        var decision = ItemTransfer.Decide(hand, 99, level, StoreKind.ShelfLevel, AimPart.Stock, shift: false);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.TakeOne, 1));
        AssertThat(ItemTransfer.Apply(decision, hand, level)).IsEqual(1);
        AssertThat(level.Count).IsEqual(1);
        AssertThat(hand.Peek()).IsEqual(Card(3));
    }

    [TestCase]
    public static void AClickOnStockWithAFullHandTakesNothing()
    {
        var decision = ItemTransfer.Decide(Hand(Card(1)), 0, Store(Card(2)), StoreKind.Pile, AimPart.Stock, false);

        AssertThat(decision).IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void AClickOnStockWhoseNextItemTheHandRefusesTakesNothing()
    {
        // A booster box needs an empty hand, so a hand holding a card takes none, plain or with Shift.
        var hand = Hand(Card(1));
        var rack = Store(BoosterBox(7));

        AssertThat(ItemTransfer.Decide(hand, 99, rack, StoreKind.ShelfLevel, AimPart.Stock, shift: false))
            .IsEqual(TransferDecision.None);
        AssertThat(ItemTransfer.Decide(hand, 99, rack, StoreKind.ShelfLevel, AimPart.Stock, shift: true))
            .IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void APlainClickOnAPileTakesOneCard()
    {
        var hand = Hand();
        var pile = Store(Cards(20));

        var decision = ItemTransfer.Decide(hand, 100, pile, StoreKind.Pile, AimPart.Stock, shift: false);

        AssertThat(ItemTransfer.Apply(decision, hand, pile)).IsEqual(1);
        AssertThat(pile.Count).IsEqual(19);
        AssertThat(hand.Count).IsEqual(1);
    }

    [TestCase]
    public static void ADeckIsTakenWholeWhenEveryCardFits()
    {
        var hand = Hand();
        var deck = Store(Cards(30));

        var decision = ItemTransfer.Decide(hand, 100, deck, StoreKind.Deck, AimPart.Stock, shift: false);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.TakeWhole, 30));
        AssertThat(ItemTransfer.Apply(decision, hand, deck)).IsEqual(30);
        AssertThat(deck.Count).IsEqual(0);
    }

    // An unstrapped deck keeps its order in the hand: the first card strapped is the first card held.
    [TestCase]
    public static void AWholeTakeKeepsTheStoresOrder()
    {
        var hand = Hand(Card(-1));
        var deck = Store(Cards(3));

        ItemTransfer.Apply(new TransferDecision(TransferMove.TakeWhole, 3), hand, deck);

        AssertThat(hand.Items).IsEqual(new[] { Card(-1), Card(0), Card(1), Card(2) });
    }

    [TestCase]
    public static void ADeckThatDoesNotFitIsNotTaken()
    {
        var decision = ItemTransfer.Decide(Hand(), 29, Store(Cards(30)), StoreKind.Deck, AimPart.Stock, false);

        AssertThat(decision).IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void PilesAndDecksHaveNoFreeSpace()
    {
        // The fake accepts a card, as a real pile does for a dropped one, so only the missing free space refuses.
        foreach (var kind in new[] { StoreKind.Pile, StoreKind.Deck })
        {
            var store = Store(Cards(5));
            AssertThat(store.CanPut(Card(99))).IsTrue();

            AssertThat(ItemTransfer.Decide(Hand(Card(99)), 99, store, kind, AimPart.Space, shift: false))
                .OverrideFailureMessage(kind.ToString())
                .IsEqual(TransferDecision.None);
        }
    }

    [TestCase]
    public static void ShiftTakesUpToTheRoomInTheHand()
    {
        var hand = Hand(Cards(70));
        var pile = Store(Cards(80));

        var decision = ItemTransfer.Decide(hand, 30, pile, StoreKind.Pile, AimPart.Stock, shift: true);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.TakeMany, 30));
        AssertThat(ItemTransfer.Apply(decision, hand, pile)).IsEqual(30);
        AssertThat(pile.Count).IsEqual(50);
    }

    [TestCase]
    public static void ShiftTakesAtMostOneHundred()
    {
        var hand = Hand();
        var box = Store(Cards(300));

        var decision = ItemTransfer.Decide(hand, 150, box, StoreKind.OpenBox, AimPart.Space, shift: true);

        AssertThat(decision).IsEqual(new TransferDecision(TransferMove.TakeMany, ItemTransfer.MostPerShiftClick));
        AssertThat(ItemTransfer.Apply(decision, hand, box)).IsEqual(100);
        AssertThat(box.Count).IsEqual(200);
    }

    [TestCase]
    public static void ShiftOnAnEmptyStoreTakesNothing()
    {
        AssertThat(ItemTransfer.Decide(Hand(), 100, Store(), StoreKind.Pile, AimPart.Stock, shift: true))
            .IsEqual(TransferDecision.None);
    }

    [TestCase]
    public static void AShiftTakeStopsAtAnItemTheHandRefuses()
    {
        // The booster box lies under two cards; it is the third item a take would reach.
        var hand = Hand();
        var box = Store(BoosterBox(1), Card(1), Card(2));

        var decision = ItemTransfer.Decide(hand, 100, box, StoreKind.OpenBox, AimPart.Space, shift: true);

        AssertThat(decision.Count).IsEqual(3);
        AssertThat(ItemTransfer.Apply(decision, hand, box)).IsEqual(2);
        AssertThat(box.Peek()).IsEqual(BoosterBox(1));
    }

    [TestCase]
    public static void ApplyingNoneMovesNothing()
    {
        var hand = Hand(Card(1));
        var store = Store(Card(2));

        AssertThat(ItemTransfer.Apply(TransferDecision.None, hand, store)).IsEqual(0);
        AssertThat(hand.Count + store.Count).IsEqual(2);
    }

    [TestCase]
    public static void AMovePutsTheRecordTheTakeReturnedNotTheOneItPeeked()
    {
        var unseeded = new StoredContainer("res://Scenes/Shop/Items/Booster.tscn", CardContainerKind.Booster, null,
            null);
        var hand = new SeedOnTake(Hand(unseeded), 7);
        var level = new ListStore(IsBooster, 4);

        AssertThat(ItemTransfer.Apply(new TransferDecision(TransferMove.Put, 1), hand, level)).IsEqual(1);

        AssertThat(level.Peek()).IsEqual(Booster(7));
    }

    [TestCase]
    public static void AnItemTheTargetRefusesAfterAllGoesBackWhereItCameFrom()
    {
        var hand = Hand(Card(1));
        var pile = Store(Card(2), Card(3));

        var moved = ItemTransfer.Apply(new TransferDecision(TransferMove.TakeMany, 2), new RefusesPuts(hand), pile);

        AssertThat(moved).IsEqual(0);
        AssertThat(pile.Count).IsEqual(2);
        AssertThat(pile.Peek()).IsEqual(Card(3));
        AssertThat(hand.Count).IsEqual(1);
    }

    /// <summary>A store over a list that also gives up its last item a target accepts, as a held open box does.</summary>
    private sealed class PickingStore(params StoredItem[] items) : IPickingStore
    {
        private readonly List<StoredItem> _items = items.ToList();

        public StoredItem[] Items => _items.ToArray();
        public int Count => _items.Count;
        public StoredItem? Peek() => _items.Count == 0 ? null : _items[^1];
        public bool CanPut(StoredItem item) => true;

        public bool Put(StoredItem item)
        {
            _items.Add(item);
            return true;
        }

        public StoredItem? TakeLast() => TakeLast(_ => true);

        public StoredItem? PeekLast(Func<StoredItem, bool> accepts) => _items.LastOrDefault(accepts);

        public StoredItem? TakeLast(Func<StoredItem, bool> accepts)
        {
            var index = _items.FindLastIndex(item => accepts(item));
            if (index < 0)
                return null;
            var item = _items[index];
            _items.RemoveAt(index);
            return item;
        }
    }

    /// <summary>A store whose take completes a container record with a seed, as the hand's take does.</summary>
    private sealed class SeedOnTake(ListStore inner, ulong seed) : IItemStore
    {
        public int Count => inner.Count;
        public StoredItem? Peek() => inner.Peek();
        public bool CanPut(StoredItem item) => inner.CanPut(item);
        public bool Put(StoredItem item) => inner.Put(item);

        public StoredItem? TakeLast() =>
            inner.TakeLast() is StoredContainer { Seed: null } container ? container with { Seed = seed } : null;
    }

    /// <summary>A store that says it takes an item and then refuses it, as a body that fails to build does.</summary>
    private sealed class RefusesPuts(ListStore inner) : IItemStore
    {
        public int Count => inner.Count;
        public StoredItem? Peek() => inner.Peek();
        public bool CanPut(StoredItem item) => inner.CanPut(item);
        public bool Put(StoredItem item) => false;
        public StoredItem? TakeLast() => inner.TakeLast();
    }

    /// <summary>A store over a list: last in, first out, up to a capacity, accepting what a predicate allows.</summary>
    private sealed class ListStore(Func<StoredItem, bool> accepts, int capacity, IEnumerable<StoredItem>? items = null)
        : IItemStore
    {
        private readonly List<StoredItem> _items = items?.ToList() ?? new List<StoredItem>();

        public StoredItem[] Items => _items.ToArray();
        public int Count => _items.Count;

        public StoredItem? Peek() => _items.Count == 0 ? null : _items[^1];

        public bool CanPut(StoredItem item) => _items.Count < capacity && accepts(item);

        public bool Put(StoredItem item)
        {
            if (!CanPut(item))
                return false;
            _items.Add(item);
            return true;
        }

        public StoredItem? TakeLast()
        {
            var last = Peek();
            if (last != null)
                _items.RemoveAt(_items.Count - 1);
            return last;
        }
    }
}
