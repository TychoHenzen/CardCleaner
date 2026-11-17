using System;
using System.Linq;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

/// <summary>
///     The booster and pack prices: a kind asks its market price until the player sets one, a set price is kept in the
///     range the pricing screen allows, setting a price makes a new list, and a box has no price at all.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ProductPriceListTest
{
    [TestCase]
    [TestCategory("Unit")]
    public static void AKindWithNoSetPriceAsksItsMarketPrice()
    {
        var prices = ProductPriceList.MarketPrices;

        AssertThat(prices.PriceFor(CardContainerKind.Booster)).IsEqual(ProductMarketPrices.BoosterPrice);
        AssertThat(prices.PriceFor(CardContainerKind.Pack)).IsEqual(ProductMarketPrices.PackPrice);
        AssertThat(prices.SetPrices.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASetPriceIsAskedForThatKindOnly()
    {
        var prices = ProductPriceList.MarketPrices.With(CardContainerKind.Booster, 6);

        AssertThat(prices.PriceFor(CardContainerKind.Booster)).IsEqual(6);
        AssertThat(prices.PriceFor(CardContainerKind.Pack)).IsEqual(ProductMarketPrices.PackPrice);
        AssertThat(prices.SetPrices.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ASetPriceIsKeptInsideTheAllowedRange()
    {
        var prices = ProductPriceList.MarketPrices
            .With(CardContainerKind.Booster, 0)
            .With(CardContainerKind.Pack, 1_000_000);

        AssertThat(prices.PriceFor(CardContainerKind.Booster)).IsEqual(ShopPrice.Min);
        AssertThat(prices.PriceFor(CardContainerKind.Pack)).IsEqual(ShopPrice.Max);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SettingAPriceLeavesTheOriginalListUnchanged()
    {
        var original = ProductPriceList.MarketPrices;

        var changed = original.With(CardContainerKind.Booster, 6);

        AssertThat(original.PriceFor(CardContainerKind.Booster)).IsEqual(ProductMarketPrices.BoosterPrice);
        AssertThat(original.SetPrices.Count).IsEqual(0);
        AssertThat(changed.PriceFor(CardContainerKind.Booster)).IsEqual(6);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ABoxHasNoPrice()
    {
        var prices = ProductPriceList.MarketPrices;

        AssertThrown(() => prices.PriceFor(CardContainerKind.Box)).IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThrown(() => prices.With(CardContainerKind.Box, 5)).IsInstanceOf<ArgumentOutOfRangeException>();
        AssertThat(ProductPriceList.PricedKinds.Contains(CardContainerKind.Box)).IsFalse();
    }
}
