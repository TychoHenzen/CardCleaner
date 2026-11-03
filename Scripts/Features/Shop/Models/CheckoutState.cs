namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Where a checkout stands: scanning items, waiting for payment, or paid.</summary>
public enum CheckoutState
{
    Scanning,
    AwaitingPayment,
    Paid,
}
