using System;
using CardCleaner.Scripts.Core.DependencyInjection;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>Instant stand-in: credits the basket's total asking price at once and sends the customer away.</summary>
[Service(typeof(ICheckoutHandoff))]
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
