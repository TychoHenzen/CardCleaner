using System;

namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>The complaint text the day report groups a refusal under.</summary>
public static class RefusalText
{
    public const string PriceTooHigh = "price too high";
    public const string NothingInStock = "nothing in stock";

    public static string For(RefusalReason reason) => reason switch
    {
        RefusalReason.PriceTooHigh => PriceTooHigh,
        RefusalReason.NothingInStock => NothingInStock,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown refusal reason."),
    };
}
