using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class OrderCartTest
{
    private static readonly StockEntry Shelf = new("shelf", "Shelf", 10, 1);
    private static readonly StockEntry Box = new("cardboard_box", "Cardboard Box", 4, 1);
    private static readonly StockEntry Locked = new("storage_rack", "Storage Rack", 30, 5);

    [TestCase]
    [TestCategory("Unit")]
    public static void AddTwiceMergesIntoOneLineWithQuantityTwo()
    {
        var cart = new OrderCart();

        cart.TryAdd(Shelf, 1);
        cart.TryAdd(Shelf, 1);

        AssertThat(cart.Lines.Count).IsEqual(1);
        AssertThat(cart.Quantity("shelf")).IsEqual(2);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TotalSumsPriceTimesQuantity()
    {
        var cart = new OrderCart();

        cart.TryAdd(Shelf, 1);
        cart.TryAdd(Shelf, 1);
        cart.TryAdd(Box, 1);

        AssertThat(cart.Total).IsEqual(24);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void RemoveOneDropsLineAtZero()
    {
        var cart = new OrderCart();
        cart.TryAdd(Shelf, 1);

        cart.RemoveOne("shelf");

        AssertThat(cart.IsEmpty).IsTrue();
        AssertThat(cart.Quantity("shelf")).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void SetQuantityZeroRemovesLine()
    {
        var cart = new OrderCart();
        cart.TryAdd(Shelf, 1);
        cart.TryAdd(Shelf, 1);

        cart.SetQuantity("shelf", 0);

        AssertThat(cart.IsEmpty).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LockedEntryIsNotAdded()
    {
        var cart = new OrderCart();

        AssertThat(cart.TryAdd(Locked, 1)).IsFalse();

        AssertThat(cart.IsEmpty).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void QuantityIsCappedAt99()
    {
        var cart = new OrderCart();
        for (var i = 0; i < OrderCart.MaxQuantity; i++)
        {
            cart.TryAdd(Shelf, 1);
        }

        AssertThat(cart.TryAdd(Shelf, 1)).IsFalse();
        AssertThat(cart.Quantity("shelf")).IsEqual(OrderCart.MaxQuantity);

        cart.SetQuantity("shelf", 500);

        AssertThat(cart.Quantity("shelf")).IsEqual(OrderCart.MaxQuantity);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ClearEmptiesTheCart()
    {
        var cart = new OrderCart();
        cart.TryAdd(Shelf, 1);
        cart.TryAdd(Box, 1);

        cart.Clear();

        AssertThat(cart.IsEmpty).IsTrue();
        AssertThat(cart.Total).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void UnknownIdIsIgnored()
    {
        var cart = new OrderCart();
        cart.TryAdd(Shelf, 1);

        cart.RemoveOne("nope");
        cart.RemoveLine("nope");
        cart.SetQuantity("nope", 3);

        AssertThat(cart.Lines.Count).IsEqual(1);
        AssertThat(cart.Quantity("shelf")).IsEqual(1);
    }
}
