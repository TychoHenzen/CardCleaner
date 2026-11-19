using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Packs.Models;

/// <summary>
///     A card: its signature (null for a card without one), the seed of its look, and the price the player set on it
///     (null while it asks its market price). The same fields a save writes for a loose card.
/// </summary>
public sealed record StoredCard(CardSignature? Signature, int? LookSeed, int? Price) : StoredItem
{
    public override ItemShape Shape => ItemShape.Card;
}
