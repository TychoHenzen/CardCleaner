using System.Collections.Generic;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>
///     A checkout customer that kept a still copy of each item it handed over, so the counter can lay the real items
///     out for scanning. A customer without it gets plain stand-in props.
/// </summary>
public interface IBasketLooks
{
    /// <summary>
    ///     The copies in basket order (null where an item had nothing to copy). The caller owns them from then on, and
    ///     a second call returns an empty list.
    /// </summary>
    IReadOnlyList<StockItemLook?> TakeBasketLooks();
}
