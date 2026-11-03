using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>Stand-in checkout until #401: credits the basket's total asking price at once.</summary>
[Service(typeof(ICheckoutHandoff))]
internal sealed class InstantCheckout : ICheckoutHandoff
{
    private readonly IMoneyService _money;

    public InstantCheckout(IMoneyService money)
    {
        _money = money;
    }

    public void Checkout(CheckoutBasket basket)
    {
        ArgumentNullException.ThrowIfNull(basket);
        if (basket.Total > 0)
            _money.Add(basket.Total);
    }
}
