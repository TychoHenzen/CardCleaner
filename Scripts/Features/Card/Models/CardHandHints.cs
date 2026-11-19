using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.CardStacks.Models;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>The key hints while the player holds cards.</summary>
public static class CardHandHints
{
    /// <summary>
    ///     No hints for an empty hand; otherwise the drop keys (and the strap key once two cards are held), or the
    ///     release while a drop is being aimed.
    /// </summary>
    public static IReadOnlyList<ControlHint> For(int heldCount, bool aimingDrop)
    {
        if (heldCount <= 0)
            return [];
        if (aimingDrop)
            return [new ControlHint("Release RMB", heldCount == 1 ? "Set the card down" : "Set the cards down")];

        var cards = heldCount == 1 ? "1 card" : $"{heldCount} cards";
        var drop = new ControlHint("RMB", $"Hold to aim, release to set down ({cards} in hand)");
        var dropOne = new ControlHint("X", "Drop the top card");
        return heldCount < CardBundleRules.FewestToStrap
            ? [drop, dropOne]
            : [drop, dropOne, new ControlHint("V", "Strap the cards into a deck")];
    }
}
