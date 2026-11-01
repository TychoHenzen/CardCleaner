using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Why a customer declined to buy.</summary>
public enum RefusalReason
{
    PriceTooHigh,
}

/// <summary>The result of one buy decision: whether the customer buys and, when not, why.</summary>
public readonly record struct BuyOutcome(bool Buys, RefusalReason? Reason);

/// <summary>
///     Decides whether a customer buys an item at an asking price. The chance is 1 at or below the
///     market price, 0 at 1.5 times the market price or more, and linear in between. A roll in [0, 1)
///     decides it: the customer buys when the roll is below the chance.
/// </summary>
public static class BuyDecision
{
    /// <summary>Asking/market ratio at or below which every customer buys.</summary>
    public const double FullChanceRatio = 1.0;

    /// <summary>Asking/market ratio at or above which no customer buys.</summary>
    public const double ZeroChanceRatio = 1.5;

    /// <summary>The chance in [0, 1] that a customer buys at this asking price.</summary>
    public static double Chance(double asking, double market)
    {
        ValidatePrices(asking, market);

        var ratio = asking / market;
        if (ratio <= FullChanceRatio)
            return 1.0;
        if (ratio >= ZeroChanceRatio)
            return 0.0;

        return (ZeroChanceRatio - ratio) / (ZeroChanceRatio - FullChanceRatio);
    }

    /// <summary>Decides one purchase from a roll in [0, 1).</summary>
    public static BuyOutcome Decide(double asking, double market, double roll)
    {
        if (roll < 0.0 || roll >= 1.0)
            throw new ArgumentOutOfRangeException(nameof(roll), roll, "Roll must be in [0, 1).");

        var chance = Chance(asking, market);
        return roll < chance
            ? new BuyOutcome(true, null)
            : new BuyOutcome(false, RefusalReason.PriceTooHigh);
    }

    private static void ValidatePrices(double asking, double market)
    {
        if (market <= 0.0)
            throw new ArgumentOutOfRangeException(nameof(market), market, "Market price must be positive.");
        if (asking < 0.0)
            throw new ArgumentOutOfRangeException(nameof(asking), asking, "Asking price cannot be negative.");
    }
}
