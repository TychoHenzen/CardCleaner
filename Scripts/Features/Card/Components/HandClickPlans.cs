using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.CardStacks.Components;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     What a left click on an item does, for every target the hand router claims. A store (a shelf board, a pile or
///     deck, an open cardboard box standing in the world) goes through the one transfer rule (<see cref="ItemTransfer"
///     />) with the part of it the aim hit; a loose card or container is picked up under the hand's own rules; the
///     trash can takes an empty cardboard box. The plan carries the prompt and the move together, so the prompt names
///     exactly what the click does, and a click that would move nothing has no plan.
/// </summary>
internal static class HandClickPlans
{
    /// <summary>Whether the router decides clicks on <paramref name="target" />.</summary>
    public static bool Claims(IInteractable target) =>
        target is ShelfLevelTarget or CardBundle or TrashCan or CardContainer or CardController;

    /// <summary>
    ///     The click on <paramref name="target" /> aimed at <paramref name="point" />, or null when it moves nothing.
    /// </summary>
    public static ClickPlan? For(IHand hand, IInteractable target, Vector3 point, bool shift)
    {
        if (target is TrashCan)
            return Trash(hand);
        if (StoreAt(target, point) is { } aimed)
            return Store(hand, target, aimed, shift);

        return target switch
        {
            CardContainer { CanInteract: true } container when hand.CanTake(container) =>
                new ClickPlan(container.Prompt, () => hand.TryTake(container)),
            CardController { CanInteract: true } card when hand.CanTake(card) =>
                new ClickPlan(card.Prompt, () => hand.TryTake(card)),
            _ => null
        };
    }

    /// <summary>
    ///     A click on nothing usable: with an open cardboard box in front it sets the box's last item down where the
    ///     player aims.
    /// </summary>
    public static ClickPlan? ForNothing(IHand hand)
    {
        if (HeldOpenBox(hand) is not { } box)
            return null;

        var decision = ItemTransfer.DecideFromHeldBoxOntoNothing(box);
        return Plan(TransferPrompt.For(decision, box.Peek(), StoreKind.OpenBox, box.Count), hand.PutOneDown);
    }

    /// <summary>
    ///     How many items a Shift-click on <paramref name="target" /> would take now (0 when it takes none).
    /// </summary>
    public static int ShiftTakes(IHand hand, IInteractable? target, Vector3 point)
    {
        if (target == null || HeldOpenBox(hand) != null || StoreAt(target, point) is not { } aimed)
            return 0;

        var decision = ItemTransfer.Decide(hand, RoomFor(hand, aimed.Store), aimed.Store, aimed.Kind, aimed.Part, true);
        return decision.Move == TransferMove.TakeMany ? decision.Count : 0;
    }

    // The store a target fronts, its kind, and the part of it the aim hit. Piles and decks are all stock.
    private static Aimed? StoreAt(IInteractable target, Vector3 point) => target switch
    {
        ShelfLevelTarget { Level: { } level } when GodotObject.IsInstanceValid(level) =>
            new Aimed(level, StoreKind.ShelfLevel, level.PartAt(point)),
        CardBundle bundle => new Aimed(bundle, bundle.Kind == CardBundleKind.Deck ? StoreKind.Deck : StoreKind.Pile,
            AimPart.Stock),
        CardContainer { IsHeld: false, IsLidOpen: true, Storage: { } storage } =>
            new Aimed(storage, StoreKind.OpenBox, storage.PartAt(point)),
        _ => null
    };

    private static ClickPlan? Store(IHand hand, IInteractable target, Aimed aimed, bool shift)
    {
        // An open cardboard box in front is the source: a click stocks its last item the store takes.
        if (HeldOpenBox(hand) is { } box)
        {
            var fromBox = ItemTransfer.DecideFromHeldBox(box, aimed.Store, aimed.Kind);
            return Plan(TransferPrompt.For(fromBox, box.PeekLast(aimed.Store.CanPut), aimed.Kind, aimed.Store.Count),
                () => ItemTransfer.ApplyFromHeldBox(fromBox, box, aimed.Store) > 0);
        }

        if (aimed.Store is ShelfLevel level && BoxBodyOnLevel(hand, level, aimed.Part, out var boxPlan))
            return boxPlan;

        var room = RoomFor(hand, aimed.Store);
        var decision = ItemTransfer.Decide(hand, room, aimed.Store, aimed.Kind, aimed.Part, shift);
        if (decision.Move == TransferMove.PickUp)
        {
            return target is CardContainer container && hand.CanTake(container)
                ? Plan(TransferPrompt.For(decision, null, aimed.Kind, aimed.Store.Count),
                    () => hand.TryTake(container))
                : null;
        }

        var item = decision.Move == TransferMove.Put ? hand.Peek() : aimed.Store.Peek();
        return Plan(TransferPrompt.For(decision, item, aimed.Kind, aimed.Store.Count),
            () => ItemTransfer.Apply(decision, hand, aimed.Store) > 0);
    }

    // A cardboard box is no record, so it goes on and off a rack level as a body: a shut box in front goes onto free
    // space, and a box in stock comes off whole. True when the click concerns such a box, with its plan (null when the
    // hand cannot take it).
    // ASSUMPTION (Low): the transfer rule's records cannot carry a cardboard box, which a rack level holds, so a shut
    // box moves as a body and every other item as a record.
    private static bool BoxBodyOnLevel(IHand hand, ShelfLevel level, AimPart part, out ClickPlan? plan)
    {
        var boxName = CardContainerKindText.NameOf(CardContainerKind.Box);
        plan = null;
        if (part == AimPart.Space && hand.Front is CardContainer { Kind: CardContainerKind.Box } held)
        {
            if (level.CanTake(StockKind.Box))
                plan = new ClickPlan($"Put the {boxName} here", () => hand.HandOver(held) && level.TryLock(held));
            return true;
        }

        if (part != AimPart.Stock || level.Last is not CardContainer { Kind: CardContainerKind.Box } stocked)
            return false;

        if (hand.CanTake(stocked))
            plan = new ClickPlan($"Pick up {boxName}", () => hand.TryTake(stocked));
        return true;
    }

    private static ClickPlan? Trash(IHand hand)
    {
        var heldBox = hand.Front is CardContainer { Kind: CardContainerKind.Box, Storage: { } storage }
            ? storage
            : null;
        var decision = ItemTransfer.DecideTrash(heldBox);
        return Plan(TransferPrompt.For(decision, null, StoreKind.OpenBox, 0), hand.DiscardFront);
    }

    // How many of the store's next items the hand takes: a booster box fills the hand, so a Shift take of booster
    // boxes takes one.
    private static int RoomFor(IHand hand, IItemStore store) =>
        store.Peek() is { } next ? hand.RoomFor(next) : hand.Room;

    // The store of the open cardboard box in front, which is then the source of a click.
    private static CartonStorage? HeldOpenBox(IHand hand) =>
        hand.Front is CardContainer { IsLidOpen: true, Storage: { } storage } ? storage : null;

    private static ClickPlan? Plan(string? prompt, System.Func<bool> apply) =>
        prompt == null ? null : new ClickPlan(prompt, apply);

    private readonly record struct Aimed(IItemStore Store, StoreKind Kind, AimPart Part);
}
