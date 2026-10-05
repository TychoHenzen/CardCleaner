using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Buys an item: deducts its price and spawns it at the delivery point. Every call is atomic, so two
///     rapid orders charge and spawn exactly twice.
/// </summary>
public interface IOrderingService
{
    OrderResult Order(OrderItem item);
}
