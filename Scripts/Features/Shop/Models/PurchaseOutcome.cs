namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The result of asking to buy one expansion lot.</summary>
public enum PurchaseOutcome
{
    Bought,
    OutOfOrder,
    Locked,
    Unaffordable,
    AlreadyOwned,
    Unknown,
}
