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

    private readonly List<RigidBody3D> _cards = new();
    private readonly Dictionary<CardContainer, Action> _exitHandlers = new();
    private bool _processingEntry;

    [Export] public Area3D Area = null!;
    [Export] public float EjectForce = DefaultEjectForce;
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

        // Only bodies this slot takes lock. Anything else (a box on a card shelf, a card on a storage rack) is
        // ignored: it is not frozen and reparented into the slot.
        if (body is not RigidBody3D rigid || !Accepts(rigid) || _cards.Contains(rigid))
            return;

        if (_cards.Count < Capacity)
        {
            _processingEntry = true;
            CallDeferred(MethodName.LockCardDeferred, rigid);
        }
        else
        {
            EjectCard(rigid);
        }
    }

    private void LockCardDeferred(RigidBody3D body)
    {
        // A body on no collision layer was taken out of play after it entered (the restocker carries a container that
        // way), so the entry queued for it is stale.
        if (IsInstanceValid(body) && body.CollisionLayer != 0)
            TryLock(body);

        _processingEntry = false;
    }

    /// <summary>
    /// Locks a body into this slot when the slot takes that stock and has room. Returns false and leaves the
    /// body alone otherwise.
    /// </summary>
    public bool TryLock(RigidBody3D body)
    {
        if (!IsInstanceValid(body) || !Accepts(body) || _cards.Contains(body) || _cards.Count >= Capacity)
            return false;

        LockCard(body);
        _cards.Add(body);
        EmitSignal(SignalName.CardsChanged);
        return true;
    }

    // A card is a card on a card shelf; a container is taken when this slot's kind accepts its kind.
    private bool Accepts(Node3D body)
    {
        return body switch
        {
            CardController => ShelfPlacementRule.Accepts(Kind, StockKind.Card),
            CardContainer container => ShelfPlacementRule.Accepts(Kind, ShelfPlacementRule.From(container.Kind)),
            _ => false
        };
    }

    private void LockCard(RigidBody3D card)
    {
        card.Freeze = true;
        card.LinearVelocity = Vector3.Zero;
        card.AngularVelocity = Vector3.Zero;
        card.Reparent(this);
        card.GlobalPosition = GlobalPosition + PositionOffset + StackOffset * _cards.Count;
        card.GlobalRotation = GlobalRotation;

        // Listen for pickup signal
        if (card is CardController cardController) cardController.CardPickedUp += OnCardPickedUp;

        // Subscribed after the reparent above, which leaves the tree and would otherwise release the container at once.
        if (card is CardContainer container)
        {
            Action handler = () => ReleaseContainer(container);
            _exitHandlers[container] = handler;
            container.TreeExiting += handler;
        }
    }

    // A locked container that leaves the tree (a player picks it up, a restore frees it) frees this slot.
    private void ReleaseContainer(CardContainer container)
    {
        Unwatch(container);
        if (!_cards.Remove(container)) return;

        RepositionCards();
        EmitSignal(SignalName.CardsChanged);
    }

    private void OnCardPickedUp(CardController cardController)
    {
        if (!_cards.Contains(cardController)) return;

        // Disconnect from pickup signal
        cardController.CardPickedUp -= OnCardPickedUp;

        // Remove from our collection - the card has already been reparented
        _cards.Remove(cardController);

        // Reposition remaining cards
        RepositionCards();

        EmitSignal(SignalName.CardsChanged);

        ILog.Print($"DeckSlot released card due to pickup. {_cards.Count} cards remaining.");
    }

    private void RepositionCards()
    {
        for (var i = 0; i < _cards.Count; i++)
        {
            var card = _cards[i];
            card.GlobalPosition = GlobalPosition + PositionOffset + StackOffset * i;
        }
    }

    private void EjectCard(RigidBody3D card)
    {
        card.Freeze = false;
        card.ApplyImpulse(Vector3.Up * EjectForce);
    }

    // Stops listening to a locked body: the pickup signal of a card, the leave-the-tree handler of a container.
    private void Unwatch(RigidBody3D body)
    {
        if (body is CardController cardController) cardController.CardPickedUp -= OnCardPickedUp;

        if (body is CardContainer container && _exitHandlers.Remove(container, out var handler))
            container.TreeExiting -= handler;
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
