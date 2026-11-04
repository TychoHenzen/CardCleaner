namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>Why a checkout is refused. <see cref="None"/> means the checkout may go ahead.</summary>
public enum CheckoutRefusal
{
    None,
    EmptyCart,
    LockedItem,
    InsufficientFunds,
}
