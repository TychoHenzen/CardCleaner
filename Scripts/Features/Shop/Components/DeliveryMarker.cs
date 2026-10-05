using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A delivery point that can cap how many layers of ordered items stack above it, so an
///     area with a ceiling never receives an item inside the geometry.
/// </summary>
public partial class DeliveryMarker : Marker3D
{
    /// <summary>Layers of items the area holds before it is full. Zero means unlimited.</summary>
    [Export]
    public int MaxLayers { get; set; }
}
