using CardCleaner.Scripts.Features.Shop.Day;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>
///     A payable sale: the session, the front customer it belongs to, and the money service that takes the payment.
/// </summary>
internal readonly record struct CheckoutSale(CheckoutSession Session, ICheckoutCustomer Customer, IMoneyService Money);
