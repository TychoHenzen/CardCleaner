using System;
using CardCleaner.Scripts.Core.Enumeration;

namespace CardCleaner.Scripts.Features.Card.Models.Effects;

/// <summary>
///     One signature for every rarity and intensity tier, for the comparison view. Rarity comes from how far each
///     element sits from 0.5 (<c>SignatureCardHelper.DetermineRarity</c>), the tier from the mean element magnitude
///     (<see cref="CardEffectMapping.IntensityOf" />), so each cell mixes elements near 0 or 1 (many rarity points,
///     little or much intensity) with elements near 0.5 (no rarity points). A test keeps every cell on its label.
/// </summary>
public static class CardEffectComparisonGrid
{
    // Rows are rarities in CardRarity order, columns are tiers in IntensityTier order. The Common and Dormant cell
    // is the all-zero signature of an ordinary pack card.
    private static readonly float[][][] Cells =
    [
        [
            [0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f],
            [0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f],
            [0.75f, 0.75f, 0.75f, 0.75f, 0.85f, 0.85f, 0.85f, 0.85f]
        ],
        [
            [0.05f, 0.15f, 0.15f, 0.15f, 0.15f, 0.15f, 0.15f, 0.35f],
            [0.05f, 0.05f, 0.05f, 0.65f, 0.75f, 0.75f, 0.75f, 0.75f],
            [0.65f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.95f]
        ],
        [
            [0.05f, 0.05f, 0.05f, 0.15f, 0.15f, 0.15f, 0.15f, 0.15f],
            [0.05f, 0.05f, 0.05f, 0.15f, 0.85f, 0.85f, 0.85f, 0.85f],
            [0.85f, 0.85f, 0.85f, 0.85f, 0.85f, 0.95f, 0.95f, 0.95f]
        ],
        [
            [0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.15f, 0.25f],
            [0.05f, 0.15f, 0.15f, 0.95f, 0.95f, 0.95f, 0.95f, 0.95f],
            [0.15f, 0.25f, 0.95f, 0.95f, 0.95f, 0.95f, 0.95f, 0.95f]
        ],
        [
            [0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f, 0.05f],
            [0.05f, 0.05f, 0.05f, 0.05f, 0.95f, 0.95f, 0.95f, 0.95f],
            [0.95f, 0.95f, 0.95f, 0.95f, 0.95f, 0.95f, 0.95f, 0.95f]
        ]
    ];

    public static CardSignature SignatureFor(CardRarity rarity, IntensityTier tier)
    {
        if (!Enum.IsDefined(rarity))
            throw new ArgumentOutOfRangeException(nameof(rarity), rarity, "Unknown card rarity");
        if (!Enum.IsDefined(tier))
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "Unknown intensity tier");

        return new CardSignature(Cells[(int)rarity][(int)tier]);
    }
}
