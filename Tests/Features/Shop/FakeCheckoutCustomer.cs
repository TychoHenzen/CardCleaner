using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>
/// A checkout customer with no body: records the queue spots it is sent to and how often it is told to leave.
/// </summary>
public sealed class FakeCheckoutCustomer : ICheckoutCustomer
{
    public FakeCheckoutCustomer(PaymentMethod method, params int[] askingPrices)
    {
        Method = method;
        var offers = askingPrices.Select((price, i) => new ShelfOffer($"fake/{i}", price, price)).ToList();
        Basket = new CheckoutBasket(offers);
    }

    public CheckoutBasket Basket { get; }

    public PaymentMethod Method { get; }

    public bool Waiting { get; set; } = true;

    public List<QueueSpot> Spots { get; } = new();

    public int LeaveCount { get; private set; }

    public void QueueAt(QueueSpot spot)
    {
        Spots.Add(spot);
    }

    public void Leave()
    {
        LeaveCount++;
        Waiting = false;
    }
}
