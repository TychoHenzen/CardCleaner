using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Takes a customer's basket at the counter and pays for it. #401 replaces the stand-in implementation.
/// </summary>
public interface ICheckoutHandoff
{
    void Checkout(CheckoutBasket basket);
}
