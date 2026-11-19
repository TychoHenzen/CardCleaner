using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;
using CardController = CardCleaner.Scripts.Features.Card.Controllers.CardController;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class DeckSlot : Node3D
{
    [Signal]
    public delegate void CardsChangedEventHandler();

    // Default values for exported properties
    private const float DefaultEjectForce = 2f;
    private const int DefaultCapacity = 5;
    private const ShelfKind DefaultKind = ShelfKind.CardShelf;
    private static readonly Vector3 DefaultStackOffset = new(0, 0.02f, 0);

    // ASSUMPTION (Low): a locked body is saved on its seat to float precision, and the seats of different slots lie
    // far more than this apart, so 5 cm tells a body on a seat from one lying beside the shelf.
    private const float SeatTolerance = 0.05f;

    private readonly List<RigidBody3D> _cards = new();
    private readonly Dictionary<RigidBody3D, Action> _exitHandlers = new();
    private bool _processingEntry;

    [Export] public Area3D Area = null!;
    [Export] public float EjectForce = DefaultEjectForce;

    // Where the first locked body sits and how far each next one stacks, both in this slot's own frame, so stock
    // stays on the tray of a shelf that is turned or moved (see SeatPosition).
    [Export] public Vector3 PositionOffset = Vector3.Zero;
    [Export] public Vector3 StackOffset = DefaultStackOffset;
    [Export] public int Capacity { get; set; } = DefaultCapacity;

    // The kind of shelf this slot sits on, which decides the stock it takes (see ShelfPlacementRule).
    [Export] public ShelfKind Kind { get; set; } = DefaultKind;

    public bool HasCards => _cards.Count > 0;

    // Convenience property for single-card usage (when Capacity = 1)
    public bool HasCard => HasCards;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(EjectForce) => true,
            nameof(StackOffset) => true,
            nameof(PositionOffset) => true,
            nameof(Capacity) => true,
            nameof(Kind) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(EjectForce) => DefaultEjectForce,
            nameof(StackOffset) => Variant.From(DefaultStackOffset),
            nameof(PositionOffset) => Variant.From(Vector3.Zero),
            nameof(Capacity) => DefaultCapacity,
            nameof(Kind) => (int)DefaultKind,
            _ => base._PropertyGetRevert(property)
        };
    }

    public override void _Ready()
    {
        Area.BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_processingEntry) return;

        // Only free stock this slot takes locks (see IsFreeStock). Anything else (a box on a card shelf, a card on a
        // storage rack, a card the player just took into the hand) is ignored: it is not frozen and reparented into
        // the slot.
        if (body is not RigidBody3D rigid || StockOf(rigid) is not { } stock || !Accepts(stock)
            || _cards.Contains(rigid) || !IsFreeStock(rigid))
            return;

        if (!HasRoomFor(stock))
        {
            EjectCard(rigid);
            return;
        }

        _processingEntry = true;
        CallDeferred(MethodName.LockCardDeferred, rigid);
    }

    private void LockCardDeferred(RigidBody3D body)
    {
        // TryLock checks again that the body is free stock: it may have been taken out of play (into a hand) between
        // the entry and this call, which makes the queued entry stale.
        TryLock(body);

        _processingEntry = false;
    }

    /// <summary>
    /// Locks a body into this slot when the slot takes that stock and has room. Returns false and leaves the
    /// body alone otherwise.
    /// </summary>
    /// <remarks>
    /// The body joins the slot before it is reparented. A reparent reports the body entering every slot trigger it
    /// overlaps again, at once, so it must already count as held there; and no slot takes a body another slot holds.
    /// Without both, two slots whose triggers reach the same body take it from each other without end, each steal
    /// queueing the next, until Godot's deferred-call queue overflows (DeckSlotContainerTest).
    /// </remarks>
    public bool TryLock(RigidBody3D body)
    {
        if (!CanTake(body))
            return false;

        var seat = _cards.Count;
        _cards.Add(body);
        LockCard(body, seat);
        EmitSignal(SignalName.CardsChanged);
        return true;
    }

    /// <summary>
    ///     True while the slot can take one more body: it is empty, or holds fewer of its stock than that stock's
    ///     limit (<see cref="SeatsFor" />).
    /// </summary>
    public bool HasRoom => _cards.Count == 0 ? Capacity > 0 : StockOf(_cards[0]) is { } held && HasRoomFor(held);

    /// <summary>
    /// Whether <see cref="TryLock" /> would lock <paramref name="body" /> now: free stock (<see cref="IsFreeStock" />)
    /// it takes, with room.
    /// </summary>
    public bool CanTake(RigidBody3D body) =>
        IsFreeStock(body) && StockOf(body) is { } stock && CanTake(stock) && !_cards.Contains(body);

    /// <summary>
    ///     Whether <paramref name="body" /> is stock a slot may take: alive, in play (anything carrying a body clears
    ///     its collision layer) and in no slot. A card just taken off a seat is reported to that seat's trigger at
    ///     once, so this rule is what keeps the slot from taking it back.
    /// </summary>
    public static bool IsFreeStock(RigidBody3D body) =>
        IsInstanceValid(body) && body.CollisionLayer != 0 && !IsHeld(body);

    /// <summary>
    ///     Whether this slot takes one more body of <paramref name="stock" /> now: its shelf takes that stock, the
    ///     slot holds nothing else, and the stack is below <see cref="SeatsFor" />.
    /// </summary>
    public bool CanTake(StockKind stock) => Accepts(stock) && HasRoomFor(stock);

    /// <summary>
    ///     How many bodies of <paramref name="stock" /> this slot stacks: the shelf's stack limit for that stock
    ///     (<see cref="ShelfPlacementRule.StackLimit" />) within the slot's <see cref="Capacity" />.
    /// </summary>
    public int SeatsFor(StockKind stock) => Math.Min(Capacity, ShelfPlacementRule.StackLimit(Kind, stock) ?? Capacity);

    /// <summary>How many more bodies of <paramref name="stock" /> this slot takes now; 0 when it takes none.</summary>
    public int FreeSeatsFor(StockKind stock) => CanTake(stock) ? SeatsFor(stock) - _cards.Count : 0;

    // A slot holds one kind of stock at a time, up to that stock's seats.
    private bool HasRoomFor(StockKind stock) =>
        (_cards.Count == 0 || StockOf(_cards[0]) == stock) && _cards.Count < SeatsFor(stock);

    // A slot holds a body it has locked: the body is its child and in its list. A released body stays the slot's
    // child (see ReleaseCard) but is free again.
    private static bool IsHeld(RigidBody3D body) =>
        body.GetParent() is DeckSlot holder && holder._cards.Contains(body);

    private bool Accepts(StockKind stock) => ShelfPlacementRule.Accepts(Kind, stock);

    // What a body is as stock: a card, or a container of its kind. Null for anything else.
    private static StockKind? StockOf(Node3D body)
    {
        return body switch
        {
            CardController => StockKind.Card,
            CardContainer container => ShelfPlacementRule.From(container.Kind),
            _ => null
        };
    }

    private void LockCard(RigidBody3D card, int seat)
    {
        card.Freeze = true;
        card.LinearVelocity = Vector3.Zero;
        card.AngularVelocity = Vector3.Zero;
        card.Reparent(this);
        card.GlobalPosition = SeatPosition(seat);
        card.GlobalRotation = GlobalRotation;

        // Listen for pickup signal
        if (card is CardController cardController) cardController.CardPickedUp += OnCardPickedUp;

        // Subscribed after the reparent above, which leaves the tree and would otherwise release the body at once.
        Action handler = () => Release(card);
        _exitHandlers[card] = handler;
        card.TreeExiting += handler;
    }

    // A locked body that leaves the tree (a player picks it up, a load frees it), or a card that reports its pickup,
    // frees this slot. A freed body that stayed in the list would hold its seat for good.
    private void Release(RigidBody3D body)
    {
        Unwatch(body);
        if (!_cards.Remove(body)) return;

        RepositionCards();
        EmitSignal(SignalName.CardsChanged);
    }

    private void OnCardPickedUp(CardController cardController) => Release(cardController);

    private void RepositionCards()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            card.GlobalPosition = SeatPosition(i);
        }
    }

    /// <summary>
    /// The world position of the body at <paramref name="index" /> in this slot's stack: <see cref="PositionOffset" />
    /// plus <paramref name="index" /> times <see cref="StackOffset" />, both turned with the slot.
    /// </summary>
    public Vector3 SeatPosition(int index) => GlobalTransform * (PositionOffset + StackOffset * index);

    /// <summary>
    /// The index of the seat <paramref name="position" /> lies on: the nearest of this slot's seats within 5 cm of it,
    /// or null when it lies on none.
    /// </summary>
    public int? SeatAt(Vector3 position)
    {
        int? nearest = null;
        var nearestDistance = SeatTolerance;
        for (var index = 0; index < Capacity; index++)
        {
            var distance = SeatPosition(index).DistanceTo(position);
            if (distance > nearestDistance)
                continue;

            nearest = index;
            nearestDistance = distance;
        }

        return nearest;
    }

    private void EjectCard(RigidBody3D card)
    {
        card.Freeze = false;
        card.ApplyImpulse(Vector3.Up * EjectForce);
    }

    // Stops listening to a locked body: its leave-the-tree handler, and a card's pickup signal.
    private void Unwatch(RigidBody3D body)
    {
        if (body is CardController cardController) cardController.CardPickedUp -= OnCardPickedUp;

        if (_exitHandlers.Remove(body, out var handler))
            body.TreeExiting -= handler;
    }

    /// <summary>The bodies currently locked in this slot: cards, and containers on a product shelf or rack.</summary>
    public IReadOnlyList<RigidBody3D> Cards => _cards;

    /// <summary>
    /// Removes a locked card from this slot's bookkeeping and stops listening to it. The card stays
    /// parented to the slot and frozen: the caller owns what happens next (the shop register sells and
    /// frees it) and must re-home it itself if it keeps the card.
    /// </summary>
    public bool ReleaseCard(RigidBody3D card)
    {
        if (!_cards.Remove(card)) return false;

        Unwatch(card);

        RepositionCards();
        EmitSignal(SignalName.CardsChanged);
        return true;
    }

    public List<CardSignature> ConsumeAllCardSignatures()
    {
        var sigs = new List<CardSignature>();
        foreach (var c in _cards.ToList()) // ToList to avoid modification during iteration
        {
            // Disconnect from pickup signal or leave-the-tree handler before destroying
            Unwatch(c);

            // A container has no signature, so only a card adds one.
            if (c is CardController cardController) sigs.Add(cardController.Signature);
            c.QueueFree();
        }

        _cards.Clear();
        EmitSignal(SignalName.CardsChanged);
        return sigs;
    }

    // Convenience method for single-card usage (when Capacity = 1)
    public CardSignature? ConsumeCardSignature()
    {
        var allSignatures = ConsumeAllCardSignatures();
        return allSignatures.Count == 0 ? null : allSignatures[0];
    }

    /// <summary>
    /// Returns signatures of all cards in the slot without consuming them.
    /// Use this for preview/estimation purposes. Containers have no signature and are skipped.
    /// </summary>
    public List<CardSignature> GetCardSignatures()
    {
        return _cards.OfType<CardController>().Select(controller => controller.Signature).ToList();
    }

    public void Clear()
    {
        foreach (var c in _cards.ToList()) // ToList to avoid modification during iteration
        {
            // Disconnect from pickup signal or leave-the-tree handler before destroying
            Unwatch(c);

            c.QueueFree();
        }

        _cards.Clear();
        EmitSignal(SignalName.CardsChanged);
    }
}
