using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     The player's hand as a shelf slot sees it: what a stock click would put on that slot, and the move itself.
/// </summary>
public interface IStockHand
{
    /// <summary>
    ///     What a click on <paramref name="slot" /> would put there (a held shut container, one item of a held open
    ///     box, else the top card), or null when nothing in hand fits it or a drop is being aimed.
    /// </summary>
    StockOffer? OfferFor(DeckSlot slot);

    /// <summary>
    ///     Moves <see cref="OfferFor" /> from the hand into the slot; false, with nothing moved, when it cannot.
    /// </summary>
    bool TryStock(DeckSlot slot);
}
