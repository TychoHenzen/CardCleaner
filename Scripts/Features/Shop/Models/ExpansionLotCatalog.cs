using Godot;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The storefront expansion lots as authored in a resource file, one <see cref="ExpansionLotData" /> per lot.
///     Further lots are one more entry here plus an authored lot node in the shop scene.
/// </summary>
[GlobalClass]
public partial class ExpansionLotCatalog : Resource
{
    /// <summary>The lots, in any order; the purchase order is each lot's own order.</summary>
    [Export]
    public ExpansionLotData[] Lots { get; set; } = [];
}
