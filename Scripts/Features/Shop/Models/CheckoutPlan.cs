using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
///     The outcome of trying to check out a cart: the refusal, the total, how far the balance falls short and,
///     for a locked item, the id of the first locked line. Shortfall is 0 for every result except
///     <see cref="CheckoutRefusal.InsufficientFunds"/>. Pure rules, no engine types.
/// </summary>
public readonly record struct CheckoutPlan(CheckoutRefusal Refusal, int Total, int Shortfall, string? LockedId)
{
    /// <summary>
    ///     Checks the cart in a fixed order: empty cart first, then any locked line, then the balance.
    /// </summary>
    public static CheckoutPlan Evaluate(OrderCart cart, int balance, int shopLevel)
    {
        if (cart.IsEmpty)
        {
            return new CheckoutPlan(CheckoutRefusal.EmptyCart, cart.Total, 0, null);
        }

        var locked = cart.Lines.FirstOrDefault(line => line.Entry.IsLocked(shopLevel));
        if (locked is not null)
        {
            return new CheckoutPlan(CheckoutRefusal.LockedItem, cart.Total, 0, locked.Entry.Id);
        }

        var total = cart.Total;
        if (total > balance)
        {
            return new CheckoutPlan(CheckoutRefusal.InsufficientFunds, total, total - balance, null);
        }

        return new CheckoutPlan(CheckoutRefusal.None, total, 0, null);
    }
}
