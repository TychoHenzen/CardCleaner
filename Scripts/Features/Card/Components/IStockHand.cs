using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     The player's hand as a shelf slot sees it: what a stock click would put on the shelf, and the move itself.
/// </summary>
public interface IStockHand
{
    /// <summary>
    ///     The held container, else the top card in hand. Null when the hand is empty or a drop is being aimed.
    /// </summary>
    RigidBody3D? Offered { get; }

    /// <summary>Moves <see cref="Offered" /> from the hand into the slot; false, with nothing moved, when it cannot.</summary>
    bool TryStock(DeckSlot slot);
}
