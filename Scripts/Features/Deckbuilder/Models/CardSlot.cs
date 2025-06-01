using CardCleaner.Scripts.Features.Card.Models;
using Godot;
using CardController = CardCleaner.Scripts.Features.Card.Controllers.CardController;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

[Tool]
public partial class CardSlot : Node3D
{
    [Signal]
    public delegate void CardChangedEventHandler();

    private RigidBody3D _card;
    private bool _processingEntry = false;
    [Export] public Area3D Area;
    [Export] public float EjectForce = 2f;
    [Export] public Vector3 PositionOffset = Vector3.Zero;

    public bool HasCard => _card != null;

    public override void _Ready()
    {
        Area.BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_processingEntry) return;
        
        if (body is not RigidBody3D card
            || !card.Name.ToString().StartsWith("Card"))
            return;

        GD.Print("Body entered CardSlot");
        if (_card == null)
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
        if (!IsInstanceValid(card) || _card != null)
        {
            _processingEntry = false;
            return;
        }
        
        LockCard(card);
        _card = card;
        _processingEntry = false;
        EmitSignal(nameof(CardChanged));
    }

    private void LockCard(RigidBody3D card)
    {
        card.Freeze = true;
        card.LinearVelocity = Vector3.Zero;
        card.AngularVelocity = Vector3.Zero;
        card.Reparent(this);
        card.GlobalPosition = GlobalPosition + PositionOffset;
        card.GlobalRotation = GlobalRotation;

        // Listen for pickup signal
        if (card is CardController cardController)
        {
            cardController.CardPickedUp += OnCardPickedUp;
        }
    }

    private void OnCardPickedUp(CardController card)
    {
        if (card == null) return;

        // Disconnect from pickup signal
        card.CardPickedUp -= OnCardPickedUp;

        // Simply clear our reference - the card has already been reparented
        _card = null;
        EmitSignal(nameof(CardChanged));
        
        GD.Print("CardSlot released card due to pickup");
    }

    private void EjectCard(RigidBody3D card)
    {
        card.Freeze = false;
        card.ApplyImpulse(Vector3.Up * EjectForce);
    }

    public CardSignature ConsumeCardSignature()
    {
        if (_card == null)
            return null;
        var sig = _card.GetNode<CardController>(".").Signature;
        
        // Disconnect from pickup signal before destroying
        if (_card is CardController cardController)
        {
            cardController.CardPickedUp -= OnCardPickedUp;
        }
        
        _card.QueueFree();
        _card = null;
        EmitSignal(nameof(CardChanged));
        return sig;
    }

    public void Clear()
    {
        if (_card != null)
        {
            // Disconnect from pickup signal before destroying
            if (_card is CardController cardController)
            {
                cardController.CardPickedUp -= OnCardPickedUp;
            }
            
            _card.QueueFree();
            _card = null;
        }
        EmitSignal(nameof(CardChanged));
    }
}