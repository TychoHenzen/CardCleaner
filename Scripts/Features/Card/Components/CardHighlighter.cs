using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Card.Services;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

[Service(typeof(IStockHand))]
public partial class CardHighlighter : Node3D, IDependent, IStockHand
{
    private IInputService _inputService = null!;
    private IControlHints? _hints;
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
        _containerHolder.SetReferences(camera, CardHolder);
        // A held container occupies the hand, so X drops no card while one is held or a booster opens into the hand.
        CardDropper.Initialize(CardHolder, Preview, camera, () => _containerHolder.IsHolding);

        // Register card-specific input (right-click for drop preparation)
        _services.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "card_drop_prepare", MouseButton.Right, OnRightPress);
        });

        _services.Get<IControlHints>(hints => _hints = hints);

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
        _hints?.Clear(this);

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

        // The board drops repeated identical hints, so publishing every frame costs one comparison. While a container
        // occupies the hand its own hints apply, and the cards' keys do nothing, so they are not shown.
        var usableCards = _containerHolder is { IsHolding: true } ? 0 : CardHolder.HeldCount;
        _hints?.Publish(this, CardHandHints.For(usableCards, CardDropper.IsPreparingDrop));
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
        // A drop in progress keeps the hand. Cards in hand leave room only for a booster, which opens into them; a box
        // or pack click is ignored then.
        if (CardDropper.IsPreparingDrop || (CardHolder.HasCards && container.Kind != CardContainerKind.Booster))
            return;

        _containerHolder.TryPickUp(container);
    }

    public StockOffer? Offered
    {
        get
        {
            // Before _Ready (or when an export is missing) the holders are not wired, so the hand offers nothing.
            if (_containerHolder == null || CardDropper.IsPreparingDrop) return null;

            // A held container is what the hand offers, even with cards in it; a booster opening offers nothing.
            if (_containerHolder.IsHolding)
                return _containerHolder.Held is { } held ? OfferOf(held) : null;

            return CardHolder.TopCard != null ? new StockOffer(StockKind.Card) : null;
        }
    }

    // A shut container offers itself. An open box or pack offers the next item inside while any is left: an emptied
    // one is freed only at the end of the frame, so the count, not its absence, stops a second take.
    private static StockOffer? OfferOf(CardContainer held)
    {
        if (!held.IsLidOpen) return new StockOffer(ShelfPlacementRule.From(held.Kind));

        return held.Remaining > 0 && CardContainerLayout.ChildKind(held.Kind) is { } inside
            ? new StockOffer(ShelfPlacementRule.From(inside), held.Remaining)
            : null;
    }

    public bool TryStock(DeckSlot slot)
    {
        if (Offered is not { } offer || !slot.CanTake(offer.Stock)) return false;

        // Offered is null while a booster opens in the hand, so a held container here is always Held.
        if (_containerHolder.Held is { } held)
            return held.IsLidOpen ? held.TakeOneInto(slot) : _containerHolder.Drop() && slot.TryLock(held);

        var card = CardHolder.TopCard;
        return card != null && CardHolder.HandOver(card) && slot.TryLock(card);
    }

    private void OnRightPress(bool pressed)
    {
        if (pressed)
        {
            // Right press while holding a container uses it (opens or closes a box, opens a booster into the hand);
            // cards are untouched in that case, and while a booster opens the press does nothing.
            if (_containerHolder.IsHolding)
            {
                _containerHolder.UseHeld();
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