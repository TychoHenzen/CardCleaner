using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Deckbuilder.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A shelf that displays cards in its slots. Each slot is a one-card <see cref="DeckSlot" /> child:
///     a card dropped on a slot locks and shows there, and picking it up releases it again.
/// </summary>
public partial class CardShelf : RigidBody3D
{
    /// <summary>The slots of this shelf, in scene order.</summary>
    public IEnumerable<DeckSlot> Slots => GetChildren().OfType<DeckSlot>();

    /// <summary>Total number of slots.</summary>
    public int SlotCount => Slots.Count();

    /// <summary>Cards currently shown on the shelf.</summary>
    public IEnumerable<RigidBody3D> StockedCards => Slots.SelectMany(slot => slot.Cards);

    public int StockedCount => StockedCards.Count();

    /// <summary>Takes a card off whichever slot holds it. False when the card is not on this shelf.</summary>
    public bool Release(RigidBody3D card)
    {
        return Slots.Any(slot => slot.ReleaseCard(card));
    }
}
