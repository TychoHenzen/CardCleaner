using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;
using Saveable;

namespace CardCleaner.Tests.Features.Shop.Services;

/// <summary>
///     The shop's price book: it reads the wholesale cost from the order catalog, saves only the prices the player set,
///     and a load replaces every price, a corrupt one falling back to the market price.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class ShopPriceBookTest
{
    private const string WholesaleCatalog = "res://Data/Shop/BackofficeCatalog.tres";

    [TestCase]
    [TestCategory("Unit")]
    public void TheWholesaleCostComesFromTheOrderCatalog()
    {
        var book = AutoFree(new ShopPriceBook { Wholesale = GD.Load<OrderCatalog>(WholesaleCatalog) })!;
        var noCatalog = AutoFree(new ShopPriceBook())!;

        AssertBool(book.WholesaleFor(CardContainerKind.Booster) == 2).IsTrue();
        AssertBool(book.WholesaleFor(CardContainerKind.Pack) == 10).IsTrue();
        AssertBool(noCatalog.WholesaleFor(CardContainerKind.Booster).HasValue).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ASetPriceRaisesChanged()
    {
        var book = AutoFree(new ShopPriceBook())!;
        var changes = 0;
        book.Changed += () => changes++;

        book.Set(CardContainerKind.Pack, 33);

        AssertThat(changes).IsEqual(1);
        AssertThat(book.PriceFor(CardContainerKind.Pack)).IsEqual(33);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void OnlyTheSetPricesAreSaved()
    {
        var book = AutoFree(new ShopPriceBook())!;
        book.Set(CardContainerKind.Booster, 6);
        var save = new NodeSave();

        book.Save(save);

        AssertBool(save.TryGetProperty<object>("booster", out var booster)).IsTrue();
        AssertThat(booster).IsEqual(6);
        AssertBool(save.TryGetProperty<object>("pack", out _)).IsFalse();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void ALoadReplacesEveryPrice()
    {
        var book = AutoFree(new ShopPriceBook())!;
        book.Set(CardContainerKind.Booster, 6);
        book.Set(CardContainerKind.Pack, 33);
        var save = new NodeSave();
        save.SetOrAddProperty("pack", 40L);
        var changes = 0;
        book.Changed += () => changes++;

        book.Load(save);

        AssertThat(book.PriceFor(CardContainerKind.Booster)).IsEqual(ProductMarketPrices.BoosterPrice);
        AssertThat(book.PriceFor(CardContainerKind.Pack)).IsEqual(40);
        AssertThat(changes).IsEqual(1);
    }
}
