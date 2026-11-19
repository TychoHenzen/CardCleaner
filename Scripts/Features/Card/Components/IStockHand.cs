using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     The player's hand as a shelf slot sees it: what a stock click would put on that slot, and the move itself.
/// </summary>
public interface IStockHand
{
    /// <summary>
    ///     What a click on <paramref name="slot" /> would put there: the held shut container, else one item out of the
    ///     held open cardboard box (the last one in that the slot takes) or booster box, else the top card in hand. Null
    ///     when the hand is empty, a drop is being aimed, or nothing in the hand fits the slot.
    /// </summary>
    StockOffer? OfferFor(DeckSlot slot);

    /// <summary>
    ///     Moves <see cref="OfferFor" /> from the hand into the slot; false, with nothing moved, when it cannot.
    /// </summary>
    bool TryStock(DeckSlot slot);
}
