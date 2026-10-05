using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One orderable item: what the player sees in a catalog and what is spawned when it is bought.
/// </summary>
[GlobalClass]
public partial class OrderItem : Resource
{
    /// <summary>Stable identifier used in logs and tests.</summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>Name shown in the ordering UI.</summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Price in whole money units.</summary>
    [Export]
    public int Price { get; set; }

    /// <summary>Scene instanced at the delivery point when the item is ordered.</summary>
    [Export]
    public PackedScene? Scene { get; set; }
}
