using System;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
[RequireGodotRuntime]
public class ProductMarketPricesTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void PackPriceExceedsWholesaleShare()
    {
        AssertThat(ProductMarketPrices.PackPrice > 60 / 8.0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BoosterPriceExceedsWholesaleShare()
    {
        AssertThat(ProductMarketPrices.BoosterPrice > 60 / 64.0).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void BoxHasNoMarketPrice()
    {
        AssertThat(ProductMarketPrices.HasMarketPrice(CardContainerKind.Box)).IsFalse();
        AssertThrown(() => ProductMarketPrices.For(CardContainerKind.Box))
            .IsInstanceOf<ArgumentOutOfRangeException>();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SealedProductsHavePrices()
    {
        AssertThat(ProductMarketPrices.HasMarketPrice(CardContainerKind.Pack)).IsTrue();
        AssertThat(ProductMarketPrices.HasMarketPrice(CardContainerKind.Booster)).IsTrue();
    }
}
