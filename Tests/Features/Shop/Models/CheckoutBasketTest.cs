using System;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class CheckoutBasketTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void TotalIsTheSumOfTheAskingPrices()
    {
        var basket = new CheckoutBasket(new[]
        {
            new ShelfOffer("a", 30, 30),
            new ShelfOffer("b", 12, 12),
        });

        AssertThat(basket.Total).IsEqual(42);
        AssertThat(basket.Items.Count).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyBasketHasTotalZero()
    {
        var basket = new CheckoutBasket(Array.Empty<ShelfOffer>());

        AssertThat(basket.Total).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void NullItemsThrow()
    {
        AssertThrown(() => new CheckoutBasket(null!)).IsInstanceOf<ArgumentNullException>();
    }
}
