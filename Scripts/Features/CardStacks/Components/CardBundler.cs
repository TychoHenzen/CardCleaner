using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     The hand's side of decks and piles. V straps the cards in hand into a deck (<see cref="CardBundle" />), which is
///     set down in front of the player. A click on a deck unstraps it into the hand when every card fits; a click on a
///     pile takes as many cards as the hand has room for. Neither works while a container is held or a drop is aimed.
/// </summary>
public partial class CardBundler : Node, IDependent
{
    private const float DefaultSetDownDistance = 1.2f;

    [Export] public InteractionSystem InteractionSystem = null!;
    [Export] public CardHolder CardHolder = null!;
    [Export] public CardDropper CardDropper = null!;

    // ASSUMPTION: the export names the concrete ContainerHolder because Godot cannot export an interface (as in
    // CardHighlighter). This class only reads IsHolding through IContainerHolder.
    [Export] public ContainerHolder ContainerHolderNode = null!;

    /// <summary>How far in front of the camera a new deck is set down.</summary>
    [Export] public float SetDownDistance = DefaultSetDownDistance;

    private IServiceResolver? _services;
    private IInputService? _input;
    private IContainerHolder? _containers;

    public void Inject(IServiceResolver services) => _services = services;

    /// <summary>True when V would strap the hand into a deck now.</summary>
    public bool CanStrap =>
        HandIsFree && CardHolder.HeldCards.OfType<CardController>().Count() >= CardBundleRules.FewestToStrap;

    // A held container occupies the hand, and a drop being aimed keeps the cards where they are.
    private bool HandIsFree => _containers is { IsHolding: false } && !CardDropper.IsPreparingDrop;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        if (NodeExports.ExportCheck(InteractionSystem, nameof(InteractionSystem), this) ||
            NodeExports.ExportCheck(CardHolder, nameof(CardHolder), this) ||
            NodeExports.ExportCheck(CardDropper, nameof(CardDropper), this) ||
            NodeExports.ExportCheck(ContainerHolderNode, nameof(ContainerHolderNode), this))
            return;

        _containers = ContainerHolderNode;
        _services.Get<IInputService>(input =>
        {
            _input = input;
            _input.RegisterAction(this, "card_strap", Key.V, () => Strap());
        });

        foreach (var bundle in GetTree().GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>())
            Connect(bundle);
        GetTree().NodeAdded += OnNodeAdded;
    }

    public override void _ExitTree()
    {
        _input?.UnregisterAllActions(this);
        GetTree().NodeAdded -= OnNodeAdded;
        foreach (var bundle in GetTree().GetNodesInGroup(CardBundle.GroupName).OfType<CardBundle>())
        {
            bundle.TakeRequested -= OnTakeRequested;
            bundle.HandRoom = null;
        }
    }

    /// <summary>
    ///     Straps every card in hand into one deck and sets it down in front of the player, the first card picked up at
    ///     the bottom. Null when the hand holds fewer than <see cref="CardBundleRules.FewestToStrap" /> cards or is
    ///     busy.
    /// </summary>
    public CardBundle? Strap()
    {
        if (!CanStrap || _services == null || InteractionSystem.Camera is not { } eye)
            return null;

        var cards = CardHolder.HeldCards.OfType<CardController>().ToList();
        var records = new List<StoredCard>(cards.Count);
        foreach (var card in cards)
        {
            records.Add((StoredCard)StoredItemBodies.RecordOf(card));
            CardHolder.HandOver(card);
            card.GetParent()?.RemoveChild(card);
            card.QueueFree();
        }

        var deck = CardBundle.Create(CardBundleKind.Deck, records);
        _services.Get<ICardSpawner>().GetNode().AddChild(deck);
        // Turned to the player's heading, so the deck lies square to the view.
        deck.GlobalTransform = new Transform3D(new Basis(Vector3.Up, eye.GlobalRotation.Y),
            eye.GlobalPosition - eye.GlobalBasis.Z * SetDownDistance);
        return deck;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is CardBundle bundle)
            Connect(bundle);
    }

    private void Connect(CardBundle bundle)
    {
        bundle.HandRoom = () => HandIsFree ? CardHolder.Room : 0;
        bundle.TakeRequested -= OnTakeRequested;
        bundle.TakeRequested += OnTakeRequested;
    }

    // The cards come out where the bundle lies and go into the hand in the order they went into the bundle.
    private void OnTakeRequested(CardBundle bundle)
    {
        var count = bundle.TakeCount;
        if (count == 0 || _services == null)
            return;

        var spawning = _services.Get<ICardSpawningService>();
        var parent = _services.Get<ICardSpawner>().GetNode();
        var at = bundle.GlobalTransform;
        foreach (var stored in bundle.Take(count))
            if (StoredItemBodies.BuildCard(stored, parent, at, spawning) is { } card)
                CardHolder.AddCard(card);
    }
}
