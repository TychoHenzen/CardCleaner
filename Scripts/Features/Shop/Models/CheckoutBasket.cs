using System;
using System.Collections.Generic;
using System.Linq;

namespace CardCleaner.Scripts.Features.Shop.Models;

/// <summary>The items a customer took and the sum of their asking prices.</summary>
public sealed class CheckoutBasket
{
    public CheckoutBasket(IReadOnlyList<ShelfOffer> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        Items = items;
        Total = items.Sum(item => item.AskingPrice);
    }

    public IReadOnlyList<ShelfOffer> Items { get; }

    public int Total { get; }
}
