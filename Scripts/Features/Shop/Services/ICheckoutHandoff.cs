namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     Takes a customer at the checkout once it hands its basket over. The counter is the real implementation;
///     <see cref="InstantCheckout" /> is the stand-in the customer suites use.
/// </summary>
public interface ICheckoutHandoff
{
    void Checkout(ICheckoutCustomer customer);
}
