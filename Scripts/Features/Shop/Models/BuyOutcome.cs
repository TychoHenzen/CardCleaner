namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The result of one buy decision: whether the customer buys and, when not, why.</summary>
public readonly record struct BuyOutcome(bool Buys, RefusalReason? Reason);
