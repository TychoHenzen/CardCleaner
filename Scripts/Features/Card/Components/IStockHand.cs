using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     The player's hand as a shelf slot sees it: what a stock click would put on the shelf, and the move itself.
/// </summary>
public interface IStockHand
{
    /// <summary>
    ///     The held shut container, else one item out of the held open box or pack, else the top card in hand. Null
    ///     when the hand is empty, a drop is being aimed, or nothing is left in the open container.
    /// </summary>
    StockOffer? Offered { get; }

    /// <summary>
    ///     Moves <see cref="Offered" /> from the hand into the slot; false, with nothing moved, when it cannot.
    /// </summary>
    bool TryStock(DeckSlot slot);
}
