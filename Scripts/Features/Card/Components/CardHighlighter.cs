using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
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

    // ASSUMPTION: the export names the concrete ContainerHolder because Godot cannot export an interface. This class
    // only calls it through IContainerHolder (_containerHolder), so the concrete type is named in this one place.
    [Export] public ContainerHolder ContainerHolderNode = null!;
    private IContainerHolder _containerHolder = null!;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        // Get reference to the generic interaction system
        if (NodeExports.ExportCheck(CardDropper, nameof(CardDropper), this) ||
            NodeExports.ExportCheck(CardHolder, nameof(CardHolder), this) ||
            NodeExports.ExportCheck(Preview, nameof(Preview), this) ||
            NodeExports.ExportCheck(InteractionSystem, nameof(InteractionSystem), this) ||
            NodeExports.ExportCheck(ContainerHolderNode, nameof(ContainerHolderNode), this))
            return;

        _containerHolder = ContainerHolderNode;

        // Initialize card systems
        var camera = InteractionSystem.Camera;
        if (camera == null)
        {
            ILog.Error("Camera not found");
            return;
        }

        CardHolder.SetReferences(camera);
        _containerHolder.SetReferences(camera);
        CardDropper.Initialize(CardHolder, Preview, camera);

        // Register card-specific input (right-click for drop preparation)
        _services.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "card_drop_prepare", MouseButton.Right, OnRightPress);
        });

        // Listen for card interaction requests from the generic system
        ConnectToCardInteractions(_services);
        ConnectToContainers();
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

    private void ConnectToContainers()
    {
        foreach (var node in GetTree().GetNodesInGroup("Containers"))
            if (node is CardContainer container)
                ConnectToContainer(container);

        // Containers opened later (a box makes packs, a pack makes boosters) enter the tree after this node is ready.
        GetTree().NodeAdded += OnNodeAdded;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is CardContainer container)
            ConnectToContainer(container);
    }

    private void ConnectToContainer(CardContainer container)
    {
        container.PickUpRequested -= OnContainerPickUpRequested;
        container.PickUpRequested += OnContainerPickUpRequested;
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);

        // Disconnect from card signals
        var cards = GetTree().GetNodesInGroup("Cards");
        foreach (var node in cards)
            if (node is CardController card)
                card.CardInteractionRequested -= OnCardInteractionRequested;

        GetTree().NodeAdded -= OnNodeAdded;
        foreach (var node in GetTree().GetNodesInGroup("Containers"))
            if (node is CardContainer container)
                container.PickUpRequested -= OnContainerPickUpRequested;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Update drop preview while preparing drop
        if (CardDropper.IsPreparingDrop) CardDropper.UpdateDropPreview();
    }

    private void OnCardInteractionRequested(CardController card)
    {
        // A held container occupies the hand, so a card click does nothing until it is dropped or opened.
        if (_containerHolder.IsHolding) return;

        // Handle card pickup through the generic interaction system
        if (!CardDropper.IsPreparingDrop) CardHolder.AddCard(card);
    }

    private void OnContainerPickUpRequested(CardContainer container)
    {
        // Cards in hand or a drop in progress keep the hand; the container click is ignored then.
        if (CardHolder.HasCards || CardDropper.IsPreparingDrop) return;

        _containerHolder.TryPickUp(container);
    }

    private void OnRightPress(bool pressed)
    {
        if (pressed)
        {
            // Right press while holding a container drops it; cards are untouched in that case.
            if (_containerHolder.IsHolding)
            {
                _containerHolder.Drop();
                return;
            }

            CardDropper.StartDropPreparation();
        }
        else
        {
            CardDropper.CompleteDropPreparation();
        }
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