using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     A customer as the checkout sees it. <see cref="Waiting" /> is true from the moment the customer hands its
///     basket over until it leaves or gives up. <see cref="Leave" /> is called once when the visit ends: after a
///     correct payment, or when the checkout turns the customer away. The customer then walks out.
/// </summary>
public interface ICheckoutCustomer
{
    CheckoutBasket Basket { get; }

    PaymentMethod Method { get; }

    bool Waiting { get; }

    void QueueAt(QueueSpot spot);

    void Leave();
}
