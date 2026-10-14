using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardHighlighter : Node3D, IDependent
{
    private IInputService _inputService = null!;
    private IServiceResolver? _services;
    [Export] public InteractionSystem InteractionSystem = null!;
    [Export] public CardDropper CardDropper = null!;
    [Export] public CardHolder CardHolder = null!;
    [Export] public DropPreview Preview = null!;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        // Get reference to the generic interaction system
        if (ILog.ExportCheck(CardDropper, nameof(CardDropper), this) ||
            ILog.ExportCheck(CardHolder, nameof(CardHolder), this) ||
            ILog.ExportCheck(Preview, nameof(Preview), this) ||
            ILog.ExportCheck(InteractionSystem, nameof(InteractionSystem), this))
            return;

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
        _services.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "card_drop_prepare", MouseButton.Right, OnRightPress);
        });

        // Listen for card interaction requests from the generic system
        ConnectToCardInteractions(_services);
    }

    private void ConnectToCardInteractions(IServiceResolver services)
    {
        // Connect to all existing cards
        var cards = GetTree().GetNodesInGroup("Cards");
        foreach (var node in cards)
            if (node is CardController card)
                ConnectToCard(card);

        // Listen for new cards being spawned by connecting to the spawning service
        services.Get<ICardSpawningService>(spawningService =>
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