namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Why a customer declined to buy.</summary>
public enum RefusalReason
{
    PriceTooHigh,

    /// <summary>The customer looked around and found nothing on the sales shelves it could take.</summary>
    NothingInStock,
}
