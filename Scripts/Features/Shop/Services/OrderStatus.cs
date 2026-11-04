namespace CardCleaner.Scripts.Features.Shop.Services;

public enum OrderStatus
{
    Success,
    InsufficientFunds,
    InvalidItem,
    NoDeliveryPoint,
    DeliveryFull,
    Locked
}
