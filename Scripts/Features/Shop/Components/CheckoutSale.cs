using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>A payable sale: the session, the front customer it belongs to, and the money service that takes the payment.</summary>
internal readonly record struct CheckoutSale(CheckoutSession Session, ICheckoutCustomer Customer, IMoneyService Money);
