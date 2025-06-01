using System.Collections.Generic;
using System.Linq;
using Godot;
using CardController = CardCleaner.Scripts.Features.Card.Controllers.CardController;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

[Tool]
public partial class DeckSlot : Node3D
{
    [Signal]
    public delegate void CardsChangedEventHandler();

    private readonly List<RigidBody3D> _cards = new();
    private bool _processingEntry = false;

    [Export] public Area3D Area;
    [Export] public float EjectForce = 2f;
    [Export] public Vector3 StackOffset = new(0, 0.02f, 0);
    [Export] public Vector3 PositionOffset = Vector3.Zero;
    [Export] public int Capacity { get; set; } = 5;

    public bool HasCards => _cards.Count > 0;

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
            CallDeferred(nameof(LockCardDeferred), card);
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
        EmitSignal(nameof(CardsChanged));
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
        if (card is CardController cardController)
        {
            cardController.CardPickedUp += OnCardPickedUp;
        }
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
        
        EmitSignal(nameof(CardsChanged));
        
        GD.Print($"DeckSlot released card due to pickup. {_cards.Count} cards remaining.");
    }

    private void RepositionCards()
    {
        for (int i = 0; i < _cards.Count; i++)
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

    public List<Card.Models.CardSignature> ConsumeAllCardSignatures()
    {
        var sigs = new List<Card.Models.CardSignature>();
        foreach (var c in _cards.ToList()) // ToList to avoid modification during iteration
        {
            // Disconnect from pickup signal before destroying
            if (c is CardController cardController)
            {
                cardController.CardPickedUp -= OnCardPickedUp;
            }
            
            sigs.Add(c.GetNode<CardController>(".").Signature);
            c.QueueFree();
        }

        _cards.Clear();
        EmitSignal(nameof(CardsChanged));
        return sigs;
    }

    public void Clear()
    {
        foreach (var c in _cards.ToList()) // ToList to avoid modification during iteration
        {
            // Disconnect from pickup signal before destroying
            if (c is CardController cardController)
            {
                cardController.CardPickedUp -= OnCardPickedUp;
            }
            
            c.QueueFree();
        }
        _cards.Clear();
        EmitSignal(nameof(CardsChanged));
    }
}