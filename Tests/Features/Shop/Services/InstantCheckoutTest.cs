using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using CardCleaner.Tests.Features.Shop;

namespace CardCleaner.Tests.Features.Shop.Services;

[TestSuite]
[RequireGodotRuntime]
public class InstantCheckoutTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void BasketOf30And12AddsFortyTwoOnce()
    {
        var fake = new FakeMoney();
        var checkout = new InstantCheckout(fake);

        checkout.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 30, 12));

        AssertThat(fake.Added.SequenceEqual(new[] { 42 })).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyBasketAddsNothing()
    {
        var fake = new FakeMoney();
        var checkout = new InstantCheckout(fake);

        checkout.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card));

        AssertThat(fake.Added.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ZeroPricedBasketAddsNothing()
    {
        var fake = new FakeMoney();
        var checkout = new InstantCheckout(fake);

        checkout.Checkout(new FakeCheckoutCustomer(PaymentMethod.Card, 0));

        AssertThat(fake.Added.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NullCustomerThrows()
    {
        var checkout = new InstantCheckout(new FakeMoney());

        AssertThrown(() => checkout.Checkout(null!)).IsInstanceOf<ArgumentNullException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EveryCustomerIsToldToLeaveOnce()
    {
        var checkout = new InstantCheckout(new FakeMoney());
        var withItems = new FakeCheckoutCustomer(PaymentMethod.Card, 30, 12);
        var empty = new FakeCheckoutCustomer(PaymentMethod.Cash);
        var zeroPriced = new FakeCheckoutCustomer(PaymentMethod.Cash, 0);

        checkout.Checkout(withItems);
        checkout.Checkout(empty);
        checkout.Checkout(zeroPriced);

        AssertThat(withItems.LeaveCount).IsEqual(1);
        AssertThat(empty.LeaveCount).IsEqual(1);
        AssertThat(zeroPriced.LeaveCount).IsEqual(1);
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
