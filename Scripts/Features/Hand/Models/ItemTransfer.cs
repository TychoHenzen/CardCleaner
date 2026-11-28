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

    /// <param name="source">
    ///     What a put takes from: the hand, whose <see cref="IItemStore.Peek" /> is its front item, or the open
    ///     cardboard box it holds (an <see cref="IPickingStore" />), which gives its last item the store accepts on any
    ///     part of the store and whose click Shift does not change.
    /// </param>
    /// <param name="handRoom">How many more one-place items the hand takes.</param>
    /// <param name="store">The aimed store.</param>
    /// <param name="kind">The aimed store's kind.</param>
    /// <param name="part">The part of the store the aim hits.</param>
    /// <param name="shift">Whether Shift is held.</param>
    public static TransferDecision Decide(
        IItemStore source,
        int handRoom,
        IItemStore store,
        StoreKind kind,
        AimPart part,
        bool shift)
    {
        if (source is IPickingStore)
            return DecidePut(source, store, kind);
        if (shift)
            return DecideTakeMany(source, handRoom, store);
        // One item comes out of a box by holding it, so an empty hand picks an open box in the world up whole.
        if (kind == StoreKind.OpenBox && source.Count == 0)
            return new TransferDecision(TransferMove.PickUp, 1);
        return part == AimPart.Stock ? DecideTake(source, handRoom, store, kind) : DecidePut(source, store, kind);
    }

    /// <summary>
    ///     The item a put from <paramref name="source" /> into <paramref name="store" /> moves, without taking it: the
    ///     last item of an open box the store accepts, else the hand's front item; null when there is none.
    /// </summary>
    public static StoredItem? NextToPut(IItemStore source, IItemStore store) =>
        source is IPickingStore box ? box.PeekLast(store.CanPut) : source.Peek();

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
    ///     Carries <paramref name="decision" /> out and returns how many items moved. A take stops early at an item the
    ///     hand refuses, so the count can be below the decision's. The world moves (pick up, set down, discard) move no
    ///     record here; the caller carries them out.
    /// </summary>
    public static int Apply(TransferDecision decision, IItemStore source, IItemStore store) => decision.Move switch
    {
        TransferMove.Put => source is IPickingStore box ? PutFromBox(box, store) : Move(source, store, 1),
        TransferMove.TakeWhole => MoveWhole(store, source, decision.Count),
        TransferMove.TakeOne or TransferMove.TakeMany => Move(store, source, decision.Count),
        _ => 0
    };

    // The box's last item the store accepts goes in; an item the store refuses after all goes back into the box.
    private static int PutFromBox(IPickingStore box, IItemStore store)
    {
        if (box.TakeLast(store.CanPut) is not { } item)
            return 0;
        if (store.Put(item))
            return 1;

        box.Put(item);
        return 0;
    }

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

    private static TransferDecision DecidePut(IItemStore source, IItemStore store, StoreKind kind) =>
        HasSpace(kind) && NextToPut(source, store) is { } item && store.CanPut(item)
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
                if (!from.Put(item))
                    ILog.Error("An item taken from a store could not be put anywhere and was lost");
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
