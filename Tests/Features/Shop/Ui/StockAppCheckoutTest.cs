using CardCleaner.Tests.TestUtilities;
using Godot;

namespace CardCleaner.Tests.Features.Shop.Ui;

/// <summary>
/// The Stock app's checkout: what is charged and delivered, and what is refused and left in the cart.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class StockAppCheckoutTest
{
    private const string DeliveredMessage = "Ordered. Items are at the delivery point.";

    private StockAppFixture _f = null!;

    [BeforeTest]
    public void Setup()
    {
        _f = new StockAppFixture();
        AddNode(_f.World);
    }

    [AfterTest]
    public static void Teardown()
    {
        TestServices.Reset();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void CheckoutChargesTotalAndDeliversEveryUnit()
    {
        _f.App.PressAdd(0);
        _f.App.PressAdd(0);

        _f.App.PressCheckout();

        AssertThat(_f.Money.Balance).IsEqual(70);
        AssertThat(string.Join(",", _f.Ordering.Calls)).IsEqual("cheap,cheap");
        AssertThat(_f.App.MessageText).IsEqual(DeliveredMessage);
        AssertThat(_f.App.CartLineCount).IsEqual(0);
        AssertBool(_f.App.CheckoutDisabled).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void TotalAboveBalanceChargesNothingAndKeepsCart()
    {
        _f.App.PressAdd(1);
        _f.App.PressAdd(1);

        _f.App.PressCheckout();

        AssertBool(_f.App.MessageText.Contains("30")).IsTrue();
        AssertThat(_f.Ordering.Calls.Count).IsEqual(0);
        AssertThat(_f.Money.Balance).IsEqual(StockAppFixture.StartingBalance);
        AssertThat(_f.App.CartLineCount).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void LockedLineAtCheckoutIsRefusedAndNothingIsCharged()
    {
        _f.Progression.Level = 4;
        _f.Progression.RaiseChanged();
        _f.App.PressAdd(2);
        _f.Progression.Level = StockAppFixture.ShopLevel;
        _f.Progression.RaiseChanged();

        _f.App.PressCheckout();

        AssertBool(_f.App.MessageText.Contains("is locked")).IsTrue();
        AssertThat(_f.Money.Balance).IsEqual(StockAppFixture.StartingBalance);
        AssertThat(_f.Ordering.Calls.Count).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void DeliveryFullMidRunKeepsDeliveredAndLeavesRestInCart()
    {
        _f.Ordering.SucceedLimit = 1;
        _f.App.PressAdd(0);
        _f.App.PressAdd(0);

        _f.App.PressCheckout();

        AssertThat(_f.Ordering.Calls.Count).IsEqual(2);
        AssertThat(_f.Money.Balance).IsEqual(110);
        AssertThat(_f.App.CartLineCount).IsEqual(1);
        AssertThat(_f.App.CartTotalText).IsEqual("Total: 40");
        AssertBool(_f.App.MessageText.Contains("Delivered 1 of 2")).IsTrue();
    }

    [TestCase]
    [TestCategory("Unit")]
    public void SecondPressAfterSuccessChargesNothingMore()
    {
        _f.App.PressAdd(0);
        _f.App.PressCheckout();

        _f.App.PressCheckout();

        AssertThat(_f.App.MessageText).IsEqual("Your cart is empty.");
        AssertThat(_f.Money.Balance).IsEqual(110);
        AssertThat(_f.Ordering.Calls.Count).IsEqual(1);
    }

    [TestCase]
    [TestCategory("Unit")]
    public void RefusedCheckoutLeavesCartUsable()
    {
        _f.App.PressCheckout();

        AssertThat(_f.App.MessageText).IsEqual("Your cart is empty.");

        _f.App.PressAdd(0);

        AssertThat(_f.App.CartTotalText).IsEqual("Total: 40");
        AssertBool(_f.App.CheckoutDisabled).IsFalse();
    }
}
