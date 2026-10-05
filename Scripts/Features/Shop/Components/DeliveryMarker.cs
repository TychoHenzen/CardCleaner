using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A ground-level delivery area for a room with a ceiling. It holds a single layer of delivery slots,
///     so a tall item never lands inside another or the ceiling, and it reuses a slot once the item
///     delivered to it has been moved away or removed. When every slot is taken the order is refused.
/// </summary>
public partial class DeliveryMarker : Marker3D
{
}
