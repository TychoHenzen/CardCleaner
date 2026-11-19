using CardCleaner.Scripts.Features.Packs.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     What the shop pays to order one unit of a sealed product. Asking prices are not kept here: every booster and
///     pack carries its own (<c>CardContainer.ShopPrice</c>), as every card does.
/// </summary>
public interface IWholesaleCosts
{
    /// <summary>What one unit of the kind costs to order, or null when the order catalog does not sell it.</summary>
    int? WholesaleFor(CardContainerKind kind);
}
