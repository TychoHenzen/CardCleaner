using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>
///     The one rule for moving items between the hand and any store: a click on stock takes by the store's policy when
///     the hand has room, and a click on free space puts the hand's front item when the store accepts it.
/// </summary>
public static class ItemTransfer
{
    /// <summary>The most items one Shift-click moves.</summary>
    public const int MostPerShiftClick = 100;

    public static TakePolicy PolicyOf(StoreKind kind) =>
        kind == StoreKind.Deck ? TakePolicy.WholeIfFits : TakePolicy.One;

    /// <summary>Whether a click can put an item into a store of this kind.</summary>
    public static bool HasSpace(StoreKind kind) => kind is StoreKind.ShelfLevel or StoreKind.OpenBox;

    /// <param name="hand">The hand; its <see cref="IItemStore.Peek" /> is its front item.</param>
    /// <param name="handRoom">How many more one-place items the hand takes.</param>
    /// <param name="store">The aimed store.</param>
    /// <param name="kind">The aimed store's kind.</param>
    /// <param name="part">The part of the store the aim hits.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public static TransferDecision Decide(
        IItemStore hand,
        int handRoom,
        IItemStore store,
        StoreKind kind,
        AimPart part,
        bool shift)
    {
        if (shift)
            return DecideTakeMany(hand, handRoom, store);
        // One item comes out of a box by holding it, so an empty hand picks an open box in the world up whole.
        if (kind == StoreKind.OpenBox && hand.Count == 0)
            return new TransferDecision(TransferMove.PickUp, 1);
        return part == AimPart.Stock ? DecideTake(hand, handRoom, store, kind) : DecidePut(hand, store, kind);
    }

    /// <summary>
    ///     A click on a store while the hand's front item is an open cardboard box: the box, not the hand, is the
    ///     source, so the click puts the box's last item the store accepts, and never the box itself.
    /// </summary>
    // ASSUMPTION: with an open box held, the aimed part and Shift do not matter: every click on a store with free
    // space puts one item, and none takes (Risk Low). The spec says only that a click on a store puts.
    public static TransferDecision DecideFromHeldBox(IPickingStore box, IItemStore store, StoreKind kind) =>
        HasSpace(kind) && box.PeekLast(store.CanPut) != null
            ? new TransferDecision(TransferMove.Put, 1)
            : TransferDecision.None;

    /// <summary>A click on nothing while an open box is held sets its last item down, when it holds any.</summary>
    public static TransferDecision DecideFromHeldBoxOntoNothing(IItemStore box) =>
        box.Count > 0 ? new TransferDecision(TransferMove.SetDown, 1) : TransferDecision.None;

    /// <summary>
    ///     A click on the trash can, which takes only an empty cardboard box.
    /// </summary>
    /// <param name="heldBox">What the cardboard box in front holds, or null when the front is anything else.</param>
    public static TransferDecision DecideTrash(IItemStore? heldBox) =>
        heldBox is { Count: 0 } ? new TransferDecision(TransferMove.Discard, 1) : TransferDecision.None;

    /// <summary>
    ///     Carries out a put from a held open box (<see cref="DecideFromHeldBox" />) and returns how many items moved.
    /// </summary>
    public static int ApplyFromHeldBox(TransferDecision decision, IPickingStore box, IItemStore store)
    {
        if (decision.Move != TransferMove.Put || box.TakeLast(store.CanPut) is not { } item)
            return 0;
        if (store.Put(item))
            return 1;

        box.Put(item);
        return 0;
    }

    /// <summary>
    ///     Carries <paramref name="decision" /> out and returns how many items moved. A take stops early at an item the
    ///     hand refuses, so the count can be below the decision's. The world moves (pick up, set down, discard) move no
    ///     record here; the caller carries them out.
    /// </summary>
    public static int Apply(TransferDecision decision, IItemStore hand, IItemStore store) => decision.Move switch
    {
        TransferMove.Put => Move(hand, store, 1),
        TransferMove.TakeWhole => MoveWhole(store, hand, decision.Count),
        TransferMove.TakeOne or TransferMove.TakeMany => Move(store, hand, decision.Count),
        _ => 0
    };

    private static TransferDecision DecideTakeMany(IItemStore hand, int handRoom, IItemStore store)
    {
        var count = Math.Min(Math.Min(handRoom, store.Count), MostPerShiftClick);
        return count > 0 && HandTakesTop(hand, store)
            ? new TransferDecision(TransferMove.TakeMany, count)
            : TransferDecision.None;
    }

    private static TransferDecision DecideTake(IItemStore hand, int handRoom, IItemStore store, StoreKind kind)
    {
        if (handRoom < 1 || !HandTakesTop(hand, store))
            return TransferDecision.None;
        if (PolicyOf(kind) == TakePolicy.One)
            return new TransferDecision(TransferMove.TakeOne, 1);
        return handRoom >= store.Count
            ? new TransferDecision(TransferMove.TakeWhole, store.Count)
            : TransferDecision.None;
    }

    private static TransferDecision DecidePut(IItemStore hand, IItemStore store, StoreKind kind) =>
        HasSpace(kind) && hand.Peek() is { } front && store.CanPut(front)
            ? new TransferDecision(TransferMove.Put, 1)
            : TransferDecision.None;

    private static bool HandTakesTop(IItemStore hand, IItemStore store) =>
        store.Peek() is { } top && hand.CanPut(top);

    // What goes in is what the take returned, not what Peek showed: a take may complete the record (a container with
    // no seed draws one as it leaves, so it opens into the same items later). An item the target still refuses goes
    // back where it came from.
    private static int Move(IItemStore from, IItemStore to, int count)
    {
        var moved = 0;
        while (moved < count && from.Peek() is { } next && to.CanPut(next) && from.TakeLast() is { } item)
        {
            if (!to.Put(item))
            {
                from.Put(item);
                break;
            }

            moved++;
        }

        return moved;
    }

    // A whole store comes out in the order its items went in, so a deck unstrapped into the hand keeps its order. The
    // items are taken first (last in first) and then put in reverse; one the target refuses after all goes back to the
    // source, and is reported lost when the source, emptied and gone, refuses it too.
    private static int MoveWhole(IItemStore from, IItemStore to, int count)
    {
        var taken = new List<StoredItem>(count);
        while (taken.Count < count && from.Peek() is { } next && to.CanPut(next) && from.TakeLast() is { } item)
            taken.Add(item);

        var moved = 0;
        var lost = 0;
        for (var i = taken.Count - 1; i >= 0; i--)
        {
            if (to.Put(taken[i]))
                moved++;
            else if (!from.Put(taken[i]))
                lost++;
        }

        if (lost > 0)
            ILog.Error($"{lost} items taken whole from a store could not be put anywhere and were lost");
        return moved;
    }
}
