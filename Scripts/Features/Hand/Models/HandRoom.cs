using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Hand.Models;

/// <summary>
///     How many of an item, as a record, a hand that holds <c>count</c> items with <c>room</c> places left takes: the
///     rule <see cref="HandItems{T}" /> applies to bodies. A booster box or a cardboard box goes only into an empty
///     hand and is then its only item; a card or a booster pack takes one place while there is room.
/// </summary>
public static class HandRoom
{
    public static int For(StoredItem item, int count, int room) =>
        item switch
        {
            StoredContainer { Kind: CardContainerKind.Box or CardContainerKind.Pack } => count == 0 ? 1 : 0,
            StoredCard or StoredContainer => room,
            _ => 0
        };
}
