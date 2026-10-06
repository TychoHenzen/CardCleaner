namespace CardCleaner.Scripts.Features.Shop.Services;

public enum OrderStatus
{
    Success,
    InsufficientFunds,
    InvalidItem,
    NoDeliveryPoint,
    DeliveryFull
}

/// <summary>Outcome of an order. <see cref="Spawned" /> is only set on success.</summary>
public readonly record struct OrderResult(OrderStatus Status, Godot.Node3D? Spawned = null)
{
    public bool Succeeded => Status == OrderStatus.Success;
}
