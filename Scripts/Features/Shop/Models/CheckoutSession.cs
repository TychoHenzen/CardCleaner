using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>
/// One customer's checkout: scan each basket item once, then pay once by the chosen method.
/// Holds no Godot types. A payment that is refused changes nothing.
/// </summary>
public sealed class CheckoutSession
{
    private readonly IReadOnlyList<CheckoutItem> _basket;
    private readonly PaymentMethod _method;
    private readonly HashSet<int> _scanned = new();

    public CheckoutSession(IReadOnlyList<CheckoutItem> basket, PaymentMethod method)
    {
        ArgumentNullException.ThrowIfNull(basket);
        if (basket.Count == 0)
        {
            throw new ArgumentException("A checkout needs at least one item.", nameof(basket));
        }

        var ids = new HashSet<int>();
        foreach (var item in basket)
        {
            if (item.AskingPrice <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(basket), item.AskingPrice, "Every asking price must be above zero.");
            }

            if (!ids.Add(item.Id))
            {
                throw new ArgumentException($"Item id {item.Id} appears twice in the basket.", nameof(basket));
            }
        }

        _basket = basket;
        _method = method;
        Total = basket.Sum(item => item.AskingPrice);
        Tendered = method == PaymentMethod.Cash ? PaymentRules.CashTendered(Total) : 0;
    }

    public int Total { get; }

    /// <summary>The cash the customer hands over; zero for a card payment.</summary>
    public int Tendered { get; }

    public CheckoutState State { get; private set; } = CheckoutState.Scanning;

    public int ScannedCount => _scanned.Count;

    /// <summary>Marks an item as scanned. False for an unknown id, a repeat, or once scanning has finished.</summary>
    public bool Scan(int itemId)
    {
        if (State != CheckoutState.Scanning || !_basket.Any(item => item.Id == itemId) || !_scanned.Add(itemId))
        {
            return false;
        }

        if (_scanned.Count == _basket.Count)
        {
            State = CheckoutState.AwaitingPayment;
        }

        return true;
    }

    /// <summary>Pays by card with the typed total. False unless a card checkout awaits payment and matches.</summary>
    public bool TryPayCard(int typedTotal)
    {
        if (_method != PaymentMethod.Card || State != CheckoutState.AwaitingPayment)
        {
            return false;
        }

        if (!PaymentRules.CardTotalMatches(typedTotal, Total))
        {
            return false;
        }

        State = CheckoutState.Paid;
        return true;
    }

    /// <summary>Pays by cash with the picked change. False unless a cash checkout awaits payment and matches.</summary>
    public bool TryPayCash(IReadOnlyCollection<int> pickedChange)
    {
        if (_method != PaymentMethod.Cash || State != CheckoutState.AwaitingPayment)
        {
            return false;
        }

        if (!PaymentRules.ChangeMatches(pickedChange, Tendered, Total))
        {
            return false;
        }

        State = CheckoutState.Paid;
        return true;
    }
}
