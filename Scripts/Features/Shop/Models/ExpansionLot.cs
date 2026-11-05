namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     One buyable storefront expansion lot. Pure data, no engine types.
/// </summary>
/// <param name="Id">The lot's id, unique in its table.</param>
/// <param name="Order">The purchase order; lots are bought in ascending order.</param>
/// <param name="Price">What the lot costs to buy.</param>
/// <param name="UnlockId">The unlock id whose shop level must be reached before the lot can be bought.</param>
/// <param name="DailyRent">The rent charged each day once the lot is owned.</param>
/// <param name="ExtraCustomers">The customers per day the lot adds once it is owned.</param>
/// <param name="DisplayName">The name shown on the bill line.</param>
/// <param name="StorefrontMinX">The lot's storefront-part minimum x, in metres.</param>
public sealed record ExpansionLot(
    string Id,
    int Order,
    int Price,
    string UnlockId,
    int DailyRent,
    int ExtraCustomers,
    string DisplayName,
    float StorefrontMinX);
