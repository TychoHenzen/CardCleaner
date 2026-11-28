namespace CardCleaner.Scripts.Features.Shop.Customers;

/// <summary>Where a customer is in its visit.</summary>
public enum CustomerState
{
    Entering,
    Browsing,
    ToCounter,
    Queued,
    Leaving,
}
