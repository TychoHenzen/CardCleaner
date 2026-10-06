using CardCleaner.Scripts.Features.Shop.Components;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Buys an item: deducts its price and spawns it at a delivery point. Every call is atomic, so two
///     rapid orders charge and spawn exactly twice.
/// </summary>
public interface IOrderingService
{
    /// <param name="item">What to buy.</param>
    /// <param name="deliveryPoint">Where it appears. Null uses the service's own default delivery point.</param>
    OrderResult Order(OrderItem item, DeliveryMarker? deliveryPoint = null);
}
