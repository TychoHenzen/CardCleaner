namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>The result of one buy decision: whether the customer buys and, when not, why.</summary>
public readonly record struct BuyOutcome(bool Buys, RefusalReason? Reason);
