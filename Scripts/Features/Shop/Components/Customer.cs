using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A shop customer. It walks its planned picks on the baked navigation mesh (through a
///     <see cref="CustomerNavigator" />), takes or refuses each one, hands its basket to the checkout through
///     <see cref="Handoff" />, waits in line, walks to the despawn point and frees itself.
/// </summary>
public partial class Customer : CharacterBody3D, ICheckoutCustomer
{
    private const float DefaultSpeed = 1.6f;
    private const float DefaultStuckSeconds = 8f;

    private readonly CarriedStock _carried = new();
    private IReadOnlyList<PlannedPick> _plan = Array.Empty<PlannedPick>();
    private ShelfOfferIndex? _offers;
    private CustomerNavigator? _navigator;
    private Vector3 _counter;
    private Vector3 _despawn;
    private Walk _walk = Walk.None;
    private int _next;
    private int _visited;
    private bool _finishedRaised;

    private enum Walk
    {
        None,
        ToPick,
        ToCounter,
        ToQueue,
        ToDespawn,
    }

    [Export]
    public float Speed { get; set; } = DefaultSpeed;

    [Export]
    public float StuckSeconds { get; set; } = DefaultStuckSeconds;

    /// <summary>Set by the spawner before the customer enters the tree.</summary>
    public ICheckoutHandoff? Handoff { get; set; }

    /// <summary>Set by the spawner before the customer enters the tree.</summary>
    public IComplaintLog? Complaints { get; set; }

    public CustomerState State { get; private set; } = CustomerState.Entering;

    /// <summary>How many shelf items the customer is carrying.</summary>
    public int CarriedCount => _carried.Count;

    /// <summary>Raised once, when the visit is over for any reason (also when the node leaves the tree).</summary>
    public event Action<Customer>? Finished;

    public override void _Ready() =>
        _navigator = new CustomerNavigator(this, GetNode<NavigationAgent3D>("Agent"), Arrived, GiveUp);

    public override void _ExitTree()
    {
        _carried.Free();
        RaiseFinished();
    }

    /// <summary>
    ///     Starts the visit: walk the picks in order, then the counter when carrying, then the despawn point.
    /// </summary>
    public void Begin(IReadOnlyList<PlannedPick> plan, ShelfOfferIndex offers, Vector3 counter, Vector3 despawn)
    {
        _plan = plan;
        _offers = offers;
        _counter = counter;
        _despawn = despawn;
        _next = 0;
        _visited = 0;
        WalkToNextPick();
    }

    /// <summary>Ends the visit now: carried items go back to their slots, then the customer leaves.</summary>
    public void GiveUp()
    {
        _carried.ReturnToSlots();
        if (State == CustomerState.Leaving)
        {
            Finish();
            return;
        }

        StartLeaving();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_walk != Walk.None)
            _navigator?.Step(delta, Speed, StuckSeconds);
    }

    private void StartWalk(Walk walk, Vector3 target)
    {
        _walk = walk;
        _navigator!.Start(target);
    }

    private void StartLeaving()
    {
        State = CustomerState.Leaving;
        StartWalk(Walk.ToDespawn, _despawn);
    }

    private void WalkToNextPick()
    {
        while (_next < _plan.Count)
        {
            if (_offers!.ApproachPointFor(_plan[_next].Offer.OfferId) is { } approach)
            {
                State = _visited == 0 ? CustomerState.Entering : CustomerState.Browsing;
                StartWalk(Walk.ToPick, approach);
                return;
            }

            _next++;
        }

        if (_carried.Count > 0)
        {
            State = CustomerState.ToCounter;
            StartWalk(Walk.ToCounter, _counter);
            return;
        }

        StartLeaving();
    }

    private void Arrived()
    {
        var walk = _walk;
        _walk = Walk.None;
        Velocity = Vector3.Zero;
        switch (walk)
        {
            case Walk.ToPick:
                _visited++;
                TryPick(_plan[_next]);
                _next++;
                WalkToNextPick();
                break;
            case Walk.ToCounter:
                HandOverBasket();
                break;
            case Walk.ToQueue:
                break;
            case Walk.ToDespawn:
                Finish();
                break;
        }
    }

    // A slot whose stock is gone is skipped with no purchase and no complaint.
    private void TryPick(PlannedPick pick)
    {
        var slot = _offers!.SlotFor(pick.Offer.OfferId);
        if (slot is null || !slot.HasCards)
            return;

        if (!pick.Outcome.Buys)
        {
            Complaints?.Add(RefusalText.For(pick.Outcome.Reason ?? RefusalReason.PriceTooHigh));
            return;
        }

        _carried.TryTake(slot, pick.Offer);
    }

    private void Finish()
    {
        _walk = Walk.None;
        RaiseFinished();
        QueueFree();
    }

    private void RaiseFinished()
    {
        if (_finishedRaised)
            return;

        _finishedRaised = true;
        Finished?.Invoke(this);
    }
}
