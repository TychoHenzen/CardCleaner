using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.CardStacks.Models;
using CardCleaner.Scripts.Features.Hand.Components;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.CardStacks.Components;

/// <summary>
///     Straps the hand into a deck: V straps the cards in hand into a deck (<see cref="CardBundle" />), which is set
///     down in front of the player. It does nothing while a booster opens in the hand or a drop is aimed. Booster packs
///     held beside the cards stay in the hand when it straps. Clicks on decks and piles are the hand router's
///     (<see cref="CardHighlighter" />).
/// </summary>
public partial class CardBundler : Node, IDependent
{
    private const float DefaultSetDownDistance = 1.2f;

    [Export] public InteractionSystem InteractionSystem = null!;
    [Export] public CardDropper CardDropper = null!;

    /// <summary>The player's hand; Godot cannot export an interface, so the node is read through IHand.</summary>
    [Export] public Node3D HandNode = null!;

    /// <summary>How far in front of the camera a new deck is set down.</summary>
    [Export] public float SetDownDistance = DefaultSetDownDistance;

    private IServiceResolver? _services;
    private IInputService? _input;
    private IHand? _hand;

    public void Inject(IServiceResolver services) => _services = services;

    /// <summary>True when V would strap the hand into a deck now.</summary>
    public bool CanStrap =>
        HandIsFree && _hand!.Cards.Count >= CardBundleRules.FewestToStrap;

    // A booster opening fills the hand, and a drop being aimed keeps the cards where they are.
    private bool HandIsFree => _hand is { IsOpening: false } && !CardDropper.IsPreparingDrop;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        if (NodeExports.ExportCheck(InteractionSystem, nameof(InteractionSystem), this) ||
            NodeExports.ExportCheck(CardDropper, nameof(CardDropper), this) ||
            NodeExports.ExportCheck(HandNode, nameof(HandNode), this))
            return;

        _hand = HandNode as IHand;
        if (_hand == null)
        {
            ILog.Error($"{nameof(HandNode)} of {Name} is not a hand");
            return;
        }

        _services.Get<IInputService>(input =>
        {
            _input = input;
            _input.RegisterAction(this, "card_strap", Key.V, () => Strap());
        });
    }

    public override void _ExitTree()
    {
        _input?.UnregisterAllActions(this);
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

        var cards = _hand!.Cards.OfType<CardController>().ToList();
        var records = new List<StoredCard>(cards.Count);
        foreach (var card in cards)
        {
            records.Add((StoredCard)StoredItemBodies.RecordOf(card));
            _hand.HandOver(card);
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
}
