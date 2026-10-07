using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Services;

/// <summary>
///     Decides what is inside a booster. Each card is independently special with probability
///     1 in <see cref="SpecialCardOdds" />, so one special card per box (512 cards) is the expectation,
///     not a guarantee. Every other card is common and carries the all-zero signature.
///     The random source is injected so tests can seed it.
/// </summary>
public class CardPackGenerator(RandomNumberGenerator rng)
{
    /// <summary>A card is special with probability 1 / SpecialCardOdds (one per 8 x 8 x 8 card box).</summary>
    public const int SpecialCardOdds = CardContainerLayout.CardsPerBox;

    /// <summary>The signature of every common card: no magical potential.</summary>
    public static CardSignature CommonSignature() => new();

    /// <summary>Rolls one card: special with probability 1 / <see cref="SpecialCardOdds" />, otherwise common.</summary>
    public CardSignature NextCardSignature()
    {
        // Randi covers 2^32 values, a multiple of SpecialCardOdds, so the modulo has no bias.
        var isSpecial = rng.Randi() % SpecialCardOdds == 0;
        return isSpecial ? CardSignature.RandomSpecial(rng) : CommonSignature();
    }

    /// <summary>The signatures of the cards in one booster, in spawn order.</summary>
    public CardSignature[] OpenBooster()
    {
        var cards = new CardSignature[CardContainerLayout.ItemsPerContainer];
        for (var i = 0; i < cards.Length; i++)
            cards[i] = NextCardSignature();
        return cards;
    }

    /// <summary>Every card signature in a whole box (8 packs x 8 boosters x 8 cards), in opening order.</summary>
    public CardSignature[] OpenBox()
    {
        var cards = new CardSignature[CardContainerLayout.CardsPerBox];
        for (var i = 0; i < cards.Length; i++)
            cards[i] = NextCardSignature();
        return cards;
    }
}
