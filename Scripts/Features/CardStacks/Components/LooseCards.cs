using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     Cards lying free in the world, which a pile may take in: alive, on a collision layer, and held by no hand
///     (whose cards hang under the camera), shelf slot or booster reveal. The pile's join area takes such a card when
///     it touches the pile; <see cref="CardPileGatherer" /> also waits for it to come to rest.
/// </summary>
internal static class LooseCards
{
    /// <summary>Whether <paramref name="node" /> is in the tree and not about to be freed.</summary>
    public static bool IsAlive(Node node) => node.IsInsideTree() && !node.IsQueuedForDeletion();

    /// <summary>Whether <paramref name="card" /> lies free in the world.</summary>
    public static bool IsFree(CardController card) =>
        IsAlive(card) && card.CollisionLayer != 0 && !HasAncestor<DeckSlot>(card) && !HasAncestor<Camera3D>(card);

    /// <summary>
    ///     The records of <paramref name="cards" />, in order. The cards leave the tree at once and are freed, so no
    ///     later pass finds them again.
    /// </summary>
    public static List<StoredCard> TakeRecords(IReadOnlyList<CardController> cards)
    {
        var records = cards.Select(card => (StoredCard)StoredItemBodies.RecordOf(card)).ToList();
        foreach (var card in cards)
        {
            card.GetParent()?.RemoveChild(card);
            card.QueueFree();
        }

        return records;
    }

    private static bool HasAncestor<T>(Node node)
    {
        for (var parent = node.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is T)
                return true;

        return false;
    }
}
