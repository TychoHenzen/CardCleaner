namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Whether a checkout counter can take a payment right now, and if not, why.</summary>
public enum PaymentReadiness
{
    Ready,
    NoRegister,
    NoSale,
    CustomerLeft,
    ScanFirst,
}
