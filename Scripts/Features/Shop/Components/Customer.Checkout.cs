using System;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>The checkout side of a customer: the basket it hands over and how it waits in line.</summary>
public partial class Customer
{
    /// <summary>The items handed over at the counter; empty until the customer hands its basket over.</summary>
    public CheckoutBasket Basket { get; private set; } = new(Array.Empty<ShelfOffer>());

    /// <summary>How the customer pays. Set by the spawner before the customer enters the tree.</summary>
    public PaymentMethod Method { get; set; } = PaymentMethod.Card;

    /// <summary>True from the moment the basket is handed over until the customer leaves or gives up.</summary>
    public bool Waiting => State == CustomerState.Queued && !_finishedRaised;

    /// <summary>Walks to a point in the queue. Ignored once the customer is no longer waiting.</summary>
    public void QueueAt(QueueSpot spot)
    {
        if (!Waiting)
            return;

        StartWalk(Walk.ToQueue, new Vector3(spot.X, spot.Y, spot.Z));
    }

    /// <summary>Ends the wait: the customer walks out. Does nothing once it has left.</summary>
    public void Leave()
    {
        if (State == CustomerState.Queued)
            StartLeaving();
    }

    private void HandOverBasket()
    {
        if (Handoff is null)
        {
            ILog.Error($"{Name} has no checkout handoff; it returns what it carries.");
            _carried.ReturnToSlots();
            StartLeaving();
            return;
        }

        Basket = new CheckoutBasket(_carried.Offers());
        _carried.Free();
        State = CustomerState.Queued;
        Handoff.Checkout(this);
    }
}
