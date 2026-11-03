using System;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The complaint text the day report groups a refusal under.</summary>
public static class RefusalText
{
    public const string PriceTooHigh = "price too high";

    public static string For(RefusalReason reason) => reason switch
    {
        RefusalReason.PriceTooHigh => PriceTooHigh,
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, "Unknown refusal reason."),
    };
}
