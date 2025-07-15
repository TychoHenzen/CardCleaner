using System;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardHighlighter : Node3D
{
    private IInputService _inputService = null!;
    [Export] public InteractionSystem InteractionSystem = null!;
    [Export] public CardDropper CardDropper = null!;
    [Export] public CardHolder CardHolder = null!;
    [Export] public DropPreview Preview = null!;

    public override void _Ready()
    {
        // Get reference to the generic interaction system
        if (CardDropper == null! || CardHolder == null! || Preview == null! || InteractionSystem == null!)
        {
            ILog.Error($"Missing required Export field references");
            return;
        }

        // Initialize card systems
        var camera = InteractionSystem.Camera;
        if (camera == null)
        {
            ILog.Error("Camera not found");
            return;
        }

        CardHolder.SetReferences(camera);
        CardDropper.Initialize(CardHolder, Preview, camera);

        // Register card-specific input (right-click for drop preparation)
        ServiceLocator.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "card_drop_prepare", MouseButton.Right, OnRightPress);
        });

        // Listen for card interaction requests from the generic system
        ConnectToCardInteractions();
    }

    private void ConnectToCardInteractions()
    {
        // Connect to all existing cards
        var cards = GetTree().GetNodesInGroup("Cards");
        foreach (var node in cards)
            if (node is CardController card)
                ConnectToCard(card);

        // Listen for new cards being spawned by connecting to the spawning service
        ServiceLocator.Get<ICardSpawningService>(spawningService =>
        {
            if (spawningService is not CardSpawningService concreteService)
                return;
            concreteService.CardSpawned += OnCardSpawned;
        });

        // Set up collision layers for cards to be detected by interaction system
        SetupCardCollisionLayers();
    }

    private void ConnectToCard(CardController card)
    {
        if (!card.IsConnected(CardController.SignalName.CardInteractionRequested,
                Callable.From<CardController>(OnCardInteractionRequested)))
            card.CardInteractionRequested += OnCardInteractionRequested;
    }

    private void OnCardSpawned(CardController card)
    {
        ConnectToCard(card);
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);

        // Disconnect from card signals
        var cards = GetTree().GetNodesInGroup("Cards");
        foreach (var node in cards)
            if (node is CardController card)
                card.CardInteractionRequested -= OnCardInteractionRequested;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Update drop preview while preparing drop
        if (CardDropper.IsPreparingDrop) CardDropper.UpdateDropPreview();
    }

    private void OnCardInteractionRequested(CardController card)
    {
        // Handle card pickup through the generic interaction system
        if (!CardDropper.IsPreparingDrop) CardHolder.AddCard(card);
    }

    private void OnRightPress(bool pressed)
    {
        if (pressed)
            CardDropper.StartDropPreparation();
        else
            CardDropper.CompleteDropPreparation();
    }

    private void SetupCardCollisionLayers()
    {
        var cards = GetTree().GetNodesInGroup("Cards");
        foreach (var node in cards)
        {
            var card = (RigidBody3D)node;
            card.CollisionLayer = CardHolder.CardCollisionLayer; // Layer 2
        }
    }
}