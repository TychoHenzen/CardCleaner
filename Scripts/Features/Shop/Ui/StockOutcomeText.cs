using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Scripts.Features.Shop.Ui;

/// <summary>
///     The player-facing sentences of the Stock app's checkout: why a plan is refused and why an order stopped.
/// </summary>
internal static class StockOutcomeText
{
    public static string Refusal(CheckoutPlan plan, string lockedName) => plan.Refusal switch
    {
        CheckoutRefusal.EmptyCart => "Your cart is empty.",
        CheckoutRefusal.LockedItem => $"{lockedName} is locked. Reach a higher shop level to order it.",
        CheckoutRefusal.InsufficientFunds => $"Not enough money. You are {plan.Shortfall} short.",
        _ => string.Empty
    };

    public static string Reason(OrderStatus status) => status switch
    {
        OrderStatus.InsufficientFunds => "Not enough money.",
        OrderStatus.DeliveryFull => "The delivery area is full.",
        OrderStatus.Locked => "An item is locked at your shop level.",
        OrderStatus.InvalidItem => "That item is not sold here.",
        OrderStatus.NoDeliveryPoint => "There is no delivery point.",
        _ => "The order could not be placed."
    };
}
