using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>
///     The click prompt that names the move a transfer decision makes. It keeps today's wording where the move is
///     unchanged ("Put the card here", "Pick up card", "Unstrap the deck (3 cards)", "Take 3 cards from the pile (5 in
///     it)"). A decision that moves nothing has no prompt at all, so the aimed store is not offered as a target.
/// </summary>
public static class TransferPrompt
{
    /// <param name="decision">The move a click would make.</param>
    /// <param name="item">The first item the move takes or puts: the hand's front item for a put, the store's next
    ///     item for a take.</param>
    /// <param name="kind">The aimed store's kind.</param>
    /// <param name="storeCount">How many items the aimed store holds now.</param>
    public static string? For(TransferDecision decision, StoredItem? item, StoreKind kind, int storeCount)
    {
        if (decision.Move == TransferMove.None || decision.Count < 1)
            return null;
        // The two moves on a cardboard box itself name no item: the box is no record.
        if (decision.Move == TransferMove.PickUp)
            return $"Pick up {CardContainerKindText.NameOf(CardContainerKind.Box)}";
        if (decision.Move == TransferMove.Discard)
            return "Throw the box away";
        if (item == null)
            return null;
        return decision.Move switch
        {
            TransferMove.Put => $"Put the {NameOf(item)} here",
            TransferMove.SetDown => $"Put the {NameOf(item)} on the floor",
            TransferMove.TakeWhole when kind == StoreKind.Deck => $"Unstrap the deck ({Count(storeCount, item)})",
            TransferMove.TakeOne when kind == StoreKind.ShelfLevel => $"Pick up {NameOf(item)}",
            _ => $"Take {Count(decision.Count, item)} from {PlaceOf(kind)} ({storeCount} in it)"
        };
    }

    private static string NameOf(StoredItem item) => item switch
    {
        StoredContainer container => CardContainerKindText.NameOf(container.Kind),
        _ => "card"
    };

    // ASSUMPTION: a move of several items names them after the first one; a store mixing kinds is named by its next.
    private static string Count(int count, StoredItem item)
    {
        if (count == 1)
            return $"1 {NameOf(item)}";
        var plural = item is StoredContainer container ? CardContainerKindText.PluralOf(container.Kind) : "cards";
        return $"{count} {plural}";
    }

    private static string PlaceOf(StoreKind kind) => kind switch
    {
        StoreKind.Pile => "the pile",
        StoreKind.Deck => "the deck",
        StoreKind.ShelfLevel => "the shelf",
        _ => "the box"
    };
}
