using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One piece of furniture the shop can place: its id, its real scene and its footprint on the floor.
///     The price lives in the Furniture app's order item, not here.
/// </summary>
[GlobalClass]
public partial class FurnitureItem : Resource
{
    /// <summary>Stable identifier, matched by the order item and the unlock row.</summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>Name shown in the Furniture app.</summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The real furniture scene that is placed on the floor.</summary>
    [Export]
    public PackedScene? Scene { get; set; }

    /// <summary>Size along X at zero quarter turns, in metres.</summary>
    [Export]
    public float Width { get; set; }

    /// <summary>Size along Z at zero quarter turns, in metres.</summary>
    [Export]
    public float Depth { get; set; }

    /// <summary>Height above the floor, in metres.</summary>
    [Export]
    public float Height { get; set; }

    /// <summary>The floor footprint at zero quarter turns.</summary>
    public FurnitureFootprint Footprint => new(Width, Depth);
}
