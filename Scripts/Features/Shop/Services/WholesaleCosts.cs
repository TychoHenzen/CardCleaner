using System.Linq;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The wholesale cost of each sealed product: the price of its row in the order catalog the shop buys stock from.
///     Place it in a scene under an AutoServiceProvider. It saves nothing.
/// </summary>
[Service(typeof(IWholesaleCosts))]
public partial class WholesaleCosts : Node, IWholesaleCosts
{
    /// <summary>The catalog the shop orders stock from; its prices are the wholesale costs.</summary>
    [Export]
    public OrderCatalog? Wholesale { get; set; }

    public int? WholesaleFor(CardContainerKind kind)
    {
        var id = ProductIds.IdOf(kind);
        return Wholesale?.Items.FirstOrDefault(item => item.Id == id)?.Price;
    }
}
