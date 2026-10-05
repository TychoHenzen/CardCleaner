using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using CardController = CardCleaner.Scripts.Features.Card.Controllers.CardController;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

public partial class DeckSlot : Node3D
{
    [Signal]
    public delegate void CardsChangedEventHandler();

    // Default values for exported properties
    private const float DefaultEjectForce = 2f;
    private const int DefaultCapacity = 5;
    private static readonly Vector3 DefaultStackOffset = new(0, 0.02f, 0);

    private readonly List<RigidBody3D> _cards = new();
    private bool _processingEntry;

    [Export] public Area3D Area = null!;
    [Export] public float EjectForce = DefaultEjectForce;
    [Export] public Vector3 PositionOffset = Vector3.Zero;
    [Export] public Vector3 StackOffset = DefaultStackOffset;
    [Export] public int Capacity { get; set; } = DefaultCapacity;

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

        if (body is not RigidBody3D card
            || _cards.Contains(card)
            || !card.Name.ToString().StartsWith("Card"))
            return;

        if (_cards.Count < Capacity)
        {
            _processingEntry = true;
            CallDeferred(MethodName.LockCardDeferred, card);
        }
        else
        {
            EjectCard(card);
        }
    }

    private void LockCardDeferred(RigidBody3D card)
    {
        if (!IsInstanceValid(card) || _cards.Contains(card) || _cards.Count >= Capacity)
        {
            _processingEntry = false;
            return;
        }

        LockCard(card);
        _cards.Add(card);
        _processingEntry = false;
        EmitSignal(SignalName.CardsChanged);
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

    /// <summary>The cards currently locked in this slot.</summary>
    public IReadOnlyList<RigidBody3D> Cards => _cards;

    /// <summary>
    /// Hands a locked card back to the caller without freeing it. The caller decides what happens
    /// to the card (for example the shop register sells and frees it).
    /// </summary>
    public bool ReleaseCard(RigidBody3D card)
    {
        if (!_cards.Remove(card)) return false;

        if (card is CardController cardController) cardController.CardPickedUp -= OnCardPickedUp;

        RepositionCards();
        EmitSignal(SignalName.CardsChanged);
        return true;
    }

    public List<CardSignature> ConsumeAllCardSignatures()
    {
        var sigs = new List<CardSignature>();
        foreach (var c in _cards.ToList()) // ToList to avoid modification during iteration
        {
            // Disconnect from pickup signal before destroying
            if (c is CardController cardController) cardController.CardPickedUp -= OnCardPickedUp;

            sigs.Add(c.GetNode<CardController>(".").Signature);
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
        return allSignatures.FirstOrDefault();
    }

    /// <summary>
    /// Returns signatures of all cards in the slot without consuming them.
    /// Use this for preview/estimation purposes.
    /// </summary>
    public List<CardSignature> GetCardSignatures()
    {
        var signatures = new List<CardSignature>();
        foreach (var card in _cards)
        {
            var controller = card.GetNode<CardController>(".");
            signatures.Add(controller.Signature);
        }
        return signatures;
    }

    public void Clear()
    {
        foreach (var c in _cards.ToList()) // ToList to avoid modification during iteration
        {
            // Disconnect from pickup signal before destroying
            if (c is CardController cardController) cardController.CardPickedUp -= OnCardPickedUp;

            c.QueueFree();
        }

        _cards.Clear();
        EmitSignal(SignalName.CardsChanged);
    }
}
