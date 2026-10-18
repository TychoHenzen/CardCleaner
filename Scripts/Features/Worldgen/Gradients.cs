using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Features.Worldgen;

/// <summary>
/// Front door for the map gradients: builds the card gradient a map samples its signatures from. Map builders call this
/// instead of constructing <see cref="CardBasedGradient"/> directly.
/// </summary>
public static class Gradients
{
    public static BaselineGradient FromCards(CardSignature[] inputCards, IRandom rng)
        => new CardBasedGradient(inputCards, rng);
}
