using CardCleaner.Scripts.Features.Shop.Ordering;

namespace CardCleaner.Scripts.Features.Shop.Furniture;

/// <summary>A slot in a <see cref="DeliveryMarker" /> and how far above the marker's ground the item starts.</summary>
public readonly record struct Placement(int Slot, float Lift);
