using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class InstantCheckoutTest
{
    private static ShelfOffer Offer(string id, int asking, int market) => new(id, asking, market);

    [TestCase]
    [TestCategory("Unit")]
    public static void BasketOf30And12AddsFortyTwoOnce()
    {
        var fake = new FakeMoney();
        var checkout = new InstantCheckout(fake);

        checkout.Checkout(new CheckoutBasket(new[] { Offer("a", 30, 30), Offer("b", 12, 12) }));

        AssertThat(fake.Added.SequenceEqual(new[] { 42 })).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyBasketAddsNothing()
    {
        var fake = new FakeMoney();
        var checkout = new InstantCheckout(fake);

        checkout.Checkout(new CheckoutBasket(Array.Empty<ShelfOffer>()));

        AssertThat(fake.Added.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ZeroPricedBasketAddsNothing()
    {
        var fake = new FakeMoney();
        var checkout = new InstantCheckout(fake);

        checkout.Checkout(new CheckoutBasket(new[] { Offer("a", 0, 4) }));

        AssertThat(fake.Added.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NullBasketThrows()
    {
        var checkout = new InstantCheckout(new FakeMoney());

        AssertThrown(() => checkout.Checkout(null!)).IsInstanceOf<ArgumentNullException>();
    }

    private sealed class FakeMoney : IMoneyService
    {
        public List<int> Added { get; } = new();

        public int Balance { get; private set; }

        public event Action<int>? BalanceChanged
        {
            add { }
            remove { }
        }

        public bool TrySpend(int amount) => false;

        public void Add(int amount)
        {
            Added.Add(amount);
        }

        public bool CanAfford(int amount) => false;
    }
}
