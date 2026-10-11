namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>A slot in a <see cref="DeliveryMarker" /> and how far above the marker's ground the item starts.</summary>
public readonly record struct Placement(int Slot, float Lift);
