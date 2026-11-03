using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using CardCleaner.Scripts.Features.Shop.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Shop.Components;

/// <summary>
///     A shop customer. It walks its planned picks on the baked navigation mesh, takes or refuses each one, pays
///     at the counter through <see cref="Handoff" />, walks to the despawn point and frees itself.
/// </summary>
public partial class Customer : CharacterBody3D
{
    private const float DefaultSpeed = 1.6f;
    private const float DefaultStuckSeconds = 8f;
    private const float ArriveMeters = 0.6f;
    private const float ProgressMeters = 0.1f;
    private const int PathSettleFrames = 3;

    private readonly List<Carried> _carried = new();
    private IReadOnlyList<PlannedPick> _plan = Array.Empty<PlannedPick>();
    private ShelfOfferIndex? _offers;
    private NavigationAgent3D? _agent;
    private Vector3 _counter;
    private Vector3 _despawn;
    private Vector3 _target;
    private Vector3 _anchor;
    private Walk _walk = Walk.None;
    private int _next;
    private int _visited;
    private int _framesSinceTarget;
    private float _stuckElapsed;
    private bool _finishedRaised;

    private enum Walk
    {
        None,
        ToPick,
        ToCounter,
        ToDespawn,
    }

    // The body is held out of the scene tree while carried.
    private sealed record Carried(RigidBody3D Body, DeckSlot Slot, ShelfOffer Offer);

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

    public override void _Ready() => _agent = GetNode<NavigationAgent3D>("Agent");

    public override void _ExitTree()
    {
        FreeCarried();
        RaiseFinished();
    }

    /// <summary>Starts the visit: walk the picks in order, then the counter when carrying, then the despawn point.</summary>
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
        ReturnCarried();
        if (State == CustomerState.Leaving)
        {
            Finish();
            return;
        }

        StartLeaving();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_walk == Walk.None || _agent is null)
            return;

        _framesSinceTarget++;
        var next = _agent.GetNextPathPosition();
        if (Flat(_target - GlobalPosition).Length() <= ArriveMeters)
        {
            Arrived();
            return;
        }

        if (_framesSinceTarget > PathSettleFrames && _agent.IsNavigationFinished())
        {
            GiveUp();
            return;
        }

        var heading = Flat(next - GlobalPosition);
        Velocity = heading.LengthSquared() > 0.0001f
            ? new Vector3(heading.Normalized().X, 0f, heading.Normalized().Y) * Speed
            : Vector3.Zero;
        MoveAndSlide();
        TrackProgress(delta);
    }

    private static Vector2 Flat(Vector3 v) => new(v.X, v.Z);

    private Vector3 OnMesh(Vector3 point) => NavigationServer3D.MapGetClosestPoint(_agent!.GetNavigationMap(), point);

    private void StartWalk(Walk walk, Vector3 target)
    {
        _walk = walk;
        _target = target;
        // The baked surface lies above the customer's origin (surface y 0.5, origin y 0), and the agent compares
        // waypoints to the origin in 3D. The offset puts the agent's waypoints at the origin's height.
        _agent!.PathHeightOffset = OnMesh(GlobalPosition).Y - GlobalPosition.Y;
        _agent.TargetPosition = target;
        _framesSinceTarget = 0;
        _anchor = GlobalPosition;
        _stuckElapsed = 0f;
    }

    private void StartLeaving()
    {
        State = CustomerState.Leaving;
        StartWalk(Walk.ToDespawn, OnMesh(_despawn));
    }

    private void WalkToNextPick()
    {
        while (_next < _plan.Count)
        {
            if (_offers!.ApproachPointFor(_plan[_next].Offer.OfferId) is { } approach)
            {
                State = _visited == 0 ? CustomerState.Entering : CustomerState.Browsing;
                StartWalk(Walk.ToPick, OnMesh(approach));
                return;
            }

            _next++;
        }

        if (_carried.Count > 0)
        {
            State = CustomerState.ToCounter;
            StartWalk(Walk.ToCounter, OnMesh(_counter));
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
                Pay();
                StartLeaving();
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

        var body = slot.Cards[0];
        if (!slot.ReleaseCard(body))
            return;

        // Out of the tree, the body has no physics presence, so the slot's Area cannot claim it again.
        body.GetParent()?.RemoveChild(body);
        _carried.Add(new Carried(body, slot, pick.Offer));
    }

    private void Pay()
    {
        if (Handoff is null)
        {
            ILog.Error($"{Name} has no checkout handoff; it returns what it carries.");
            ReturnCarried();
            return;
        }

        var basket = new CheckoutBasket(_carried.Select(item => item.Offer).ToList());
        FreeCarried();
        Handoff.Checkout(basket);
    }

    private void ReturnCarried()
    {
        foreach (var item in _carried.Where(item => IsInstanceValid(item.Body)).ToList())
        {
            if (!IsInstanceValid(item.Slot))
            {
                item.Body.Free();
                continue;
            }

            item.Slot.AddChild(item.Body);
            if (!item.Slot.TryLock(item.Body))
                item.Body.Free();
        }

        _carried.Clear();
    }

    // The carried bodies are out of the tree, so QueueFree would still free them, but Free is exact and immediate.
    private void FreeCarried()
    {
        foreach (var item in _carried.Where(item => IsInstanceValid(item.Body)).ToList())
            item.Body.Free();

        _carried.Clear();
    }

    private void TrackProgress(double delta)
    {
        _stuckElapsed += (float)delta;
        if (Flat(GlobalPosition - _anchor).Length() >= ProgressMeters)
        {
            _anchor = GlobalPosition;
            _stuckElapsed = 0f;
        }
        else if (_stuckElapsed >= StuckSeconds)
        {
            GiveUp();
        }
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
