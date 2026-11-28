using System;
using CardCleaner.Scripts.Features.Shop.Day;

namespace CardCleaner.Scripts.Features.Shop.Checkout;

/// <summary>
///     Instant stand-in: credits the basket's total asking price at once and sends the customer away. Tests register
///     it by instance; it is not a scanned [Service], so the game always resolves the shop's real CheckoutCounter.
/// </summary>
internal sealed class InstantCheckout : ICheckoutHandoff
{
    private readonly IMoneyService _money;

    public InstantCheckout(IMoneyService money)
    {
        _money = money;
    }

    public void Checkout(ICheckoutCustomer customer)
    {
        ArgumentNullException.ThrowIfNull(customer);
        if (customer.Basket.Total > 0)
            _money.Add(customer.Basket.Total);

        customer.Leave();
    }
}
