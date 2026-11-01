using CardCleaner.Scripts.Core.Interfaces;

namespace CardCleaner.Scripts.Features.Card.Models;

/// <summary>
///     Picks the signature that drives a card's visuals. A card with magical potential shows its own signature;
///     an all-zero signature (an ordinary card) gets a fresh random one, so common cards do not look identical.
///     Gameplay still reads the card's own signature.
/// </summary>
public static class CardVisualSignature
{
    public static CardSignature For(CardSignature signature, IRandom random)
    {
        return signature.HasMagicalPotential() ? signature : SignatureRandom.Create(random);
    }
}
