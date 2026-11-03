using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Features.Shop.Models;

namespace CardCleaner.Scripts.Features.Shop.Services;

/// <summary>
///     The line of customers at a checkout counter, front first, with the spot each was last sent to. Extra
///     customers share the last spot. Only a customer whose spot changed is told where to stand.
/// </summary>
internal sealed class CheckoutQueue
{
    private readonly List<ICheckoutCustomer> _customers = new();
    private readonly Dictionary<ICheckoutCustomer, int> _spotOf = new();

    /// <summary>The customer at the front of the line, or null when nobody is waiting.</summary>
    public ICheckoutCustomer? Front => _customers.Count > 0 ? _customers[0] : null;

    /// <summary>Joins the back of the line. False when the customer is already in it.</summary>
    public bool Add(ICheckoutCustomer customer)
    {
        if (_customers.Contains(customer))
            return false;

        _customers.Add(customer);
        return true;
    }

    /// <summary>Takes a customer out of the line and forgets the spot they had.</summary>
    public void Remove(ICheckoutCustomer customer)
    {
        _customers.Remove(customer);
        _spotOf.Remove(customer);
    }

    /// <summary>Takes out every customer who has stopped waiting.</summary>
    public void RemoveDeparted()
    {
        foreach (var customer in _customers.Where(customer => !customer.Waiting).ToList())
            Remove(customer);
    }

    /// <summary>
    ///     Sends each customer whose spot changed to it. The spot of the customer at line position i is
    ///     <c>min(i, spotCount - 1)</c>, so the extras share the last spot. Nothing happens when there are no spots.
    /// </summary>
    public void AssignSpots(int spotCount, Func<int, QueueSpot> spotAt)
    {
        for (var i = 0; i < _customers.Count && spotCount > 0; i++)
        {
            var customer = _customers[i];
            var spot = Math.Min(i, spotCount - 1);
            if (_spotOf.TryGetValue(customer, out var current) && current == spot)
                continue;

            _spotOf[customer] = spot;
            customer.QueueAt(spotAt(spot));
        }
    }
}
