using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Tests.Features.Shop.Models;

[TestSuite]
public class CheckoutPlanTest
{
    private static readonly StockEntry Shelf = new("shelf", "Shelf", 10, 1);
    private static readonly StockEntry Locked = new("storage_rack", "Storage Rack", 30, 5);

    [TestCase]
    [TestCategory("Unit")]
    public static void EmptyCartIsRefused()
    {
        var plan = CheckoutPlan.Evaluate(new OrderCart(), 100, 1);

        AssertThat(plan.Refusal).IsEqual(CheckoutRefusal.EmptyCart);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void TotalAboveBalanceReportsShortfall()
    {
        var cart = new OrderCart();
        cart.TryAdd(Shelf, 1);
        cart.TryAdd(Shelf, 1);

        var plan = CheckoutPlan.Evaluate(cart, 15, 1);

        AssertThat(plan.Refusal).IsEqual(CheckoutRefusal.InsufficientFunds);
        AssertThat(plan.Total).IsEqual(20);
        AssertThat(plan.Shortfall).IsEqual(5);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LockedLineIsRefusedEvenWhenAffordable()
    {
        var cart = new OrderCart();
        cart.TryAdd(Locked, 5);

        var plan = CheckoutPlan.Evaluate(cart, 1000, 1);

        AssertThat(plan.Refusal).IsEqual(CheckoutRefusal.LockedItem);
        AssertThat(plan.LockedId).IsEqual("storage_rack");
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void ExactBalanceIsAllowed()
    {
        var cart = new OrderCart();
        cart.TryAdd(Shelf, 1);
        cart.TryAdd(Shelf, 1);

        var plan = CheckoutPlan.Evaluate(cart, 20, 1);

        AssertThat(plan.Refusal).IsEqual(CheckoutRefusal.None);
        AssertThat(plan.Shortfall).IsEqual(0);
    }

    [TestCase]
    [TestCategory("Unit")]
    public static void LockedBeatsInsufficientFunds()
    {
        var cart = new OrderCart();
        cart.TryAdd(Locked, 5);

        var plan = CheckoutPlan.Evaluate(cart, 1, 1);

        AssertThat(plan.Refusal).IsEqual(CheckoutRefusal.LockedItem);
        AssertThat(plan.LockedId).IsEqual("storage_rack");
    }
}
