using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One expansion lot as authored in a resource file. The shop's expansion service converts each one to the
///     engine-free <see cref="ExpansionLot" /> record.
/// </summary>
[GlobalClass]
public partial class ExpansionLotData : Resource
{
    /// <summary>Stable identifier, unique in the lots table.</summary>
    [Export]
    public string Id { get; set; } = string.Empty;

    /// <summary>The purchase order; lots are bought in ascending order.</summary>
    [Export]
    public int Order { get; set; }

    /// <summary>What the lot costs to buy, in whole money units.</summary>
    [Export]
    public int Price { get; set; }

    /// <summary>The unlock id whose shop level must be reached before the lot can be bought.</summary>
    [Export]
    public string UnlockId { get; set; } = string.Empty;

    /// <summary>The rent charged each day once the lot is owned.</summary>
    [Export]
    public int DailyRent { get; set; }

    /// <summary>The customers per day the lot adds once it is owned.</summary>
    [Export]
    public int ExtraCustomers { get; set; }

    /// <summary>The name shown on the bill line.</summary>
    [Export]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>The lot's storefront-part minimum x, in metres.</summary>
    [Export]
    public float StorefrontMinX { get; set; }
}
