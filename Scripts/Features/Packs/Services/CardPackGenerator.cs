using System.Linq;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Services;

/// <summary>
///     Decides what is inside a booster. Each card is independently special with probability
///     1 in <see cref="SpecialCardOdds" />. With 512 cards per box, about 92 percent of boxes
///     (1 - (199/200)^512) hold at least one special card; a box is not guaranteed one.
///     Every other card is common and carries the all-zero signature.
///     The random source is injected so tests can seed it.
/// </summary>
public class CardPackGenerator(IRandom rng)
{
    /// <summary>A card is special with probability 1 in 200 (about 92 percent of 512-card boxes hold one).</summary>
    public const int SpecialCardOdds = 200;

    /// <summary>The signature of every common card: no magical potential.</summary>
    public static CardSignature CommonSignature() => new();

    /// <summary>
    /// Rolls one card: special with probability 1 / <see cref="SpecialCardOdds" />, otherwise common.
    /// </summary>
    public CardSignature NextCardSignature()
    {
        // 200 does not divide 2^32, so Randi() % odds would be biased. RandiRange rejects the biased tail.
        var isSpecial = rng.RandiRange(0, SpecialCardOdds - 1) == 0;
        return isSpecial ? SignatureRandom.CreateSpecial(rng) : CommonSignature();
    }

    /// <summary>The signatures of the cards in one booster, in spawn order.</summary>
    public CardSignature[] OpenBooster() =>
        Open(CardContainerLayout.ItemsPerContainer);

    /// <summary>Every card signature in a whole box (8 packs x 8 boosters x 8 cards), in opening order.</summary>
    public CardSignature[] OpenBox() => Open(CardContainerLayout.CardsPerBox);

    private CardSignature[] Open(int count) =>
        Enumerable.Range(0, count).Select(_ => NextCardSignature()).ToArray();
}
