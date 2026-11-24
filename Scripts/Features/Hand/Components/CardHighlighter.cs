using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Player.Controllers;
using Godot;

namespace CardCleaner.Scripts.Features.Hand.Components;

/// <summary>
///     The player's hand router: the one place that decides what a left click on an item does (<see cref="IClickRouter"
///     />, plans in <see cref="HandClickPlans" />), the right button on the front item, and the hand's key hints.
///     Nothing is clicked while a booster opens in the hand or a card drop is being aimed.
/// </summary>
public partial class CardHighlighter : Node3D, IDependent, IClickRouter
{
    private IInputService _inputService = null!;
    private IControlHints? _hints;
    private IServiceResolver? _services;
    [Export] public InteractionSystem InteractionSystem = null!;
    [Export] public CardDropper CardDropper = null!;
    [Export] public DropPreview Preview = null!;

    /// <summary>The player's hand; Godot cannot export an interface, so the node is read through IHand.</summary>
    [Export] public Node3D HandNode = null!;

    private IHand? _hand;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        if (NodeExports.ExportCheck(CardDropper, nameof(CardDropper), this) ||
            NodeExports.ExportCheck(Preview, nameof(Preview), this) ||
            NodeExports.ExportCheck(InteractionSystem, nameof(InteractionSystem), this) ||
            NodeExports.ExportCheck(HandNode, nameof(HandNode), this))
            return;

        if (HandNode is not IHand hand)
        {
            ILog.Error($"{nameof(HandNode)} of {Name} is not a hand");
            return;
        }

        var camera = InteractionSystem.Camera;
        if (camera == null)
        {
            ILog.Error("Camera not found");
            return;
        }

        hand.SetReferences(camera);
        _hand = hand;
        CardDropper.Initialize(hand, Preview);

        // The right button uses the front item (see OnRightPress).
        _services.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "card_drop_prepare", MouseButton.Right, OnRightPress);
        });

        _services.Get<IControlHints>(hints => _hints = hints);

        InteractionSystem.Router = this;
        SetupCardCollisionLayers();
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);
        _hints?.Clear(this);
        if (IsInstanceValid(InteractionSystem) && InteractionSystem.Router == this)
            InteractionSystem.Router = null;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Update drop preview while preparing drop
        if (CardDropper.IsPreparingDrop) CardDropper.UpdateDropPreview();

        // The board drops repeated identical hints, so publishing every frame costs one comparison. While a booster
        // opens in the hand nothing it holds can be used, so no hint shows.
        if (_hints == null || _hand == null)
            return;

        if (_hand.IsOpening)
            _hints.Clear(this);
        else
            _hints.Publish(this, HandHints.For(HintState(_hand)));
    }

    private HandHintState HintState(IHand hand)
    {
        var front = hand.Front as CardContainer;
        var shiftTakes = HandClickPlans.ShiftTakes(hand, InteractionSystem.Target, InteractionSystem.AimPoint);
        return new HandHintState(front?.Kind, hand.Count, hand.Cards.Count, CardDropper.IsPreparingDrop,
            front?.IsLidOpen ?? false, front?.Remaining ?? 0, hand.FrontBoosterFits)
        {
            ShiftTakesMany = shiftTakes > 1
        };
    }

    public bool Claims(IInteractable target) => HandClickPlans.Claims(target);

    public ClickPlan? PlanFor(IInteractable target, Vector3 point, bool shift) =>
        ClickingHand() is { } hand ? HandClickPlans.For(hand, target, point, shift) : null;

    public ClickPlan? PlanForNothing() => ClickingHand() is { } hand ? HandClickPlans.ForNothing(hand) : null;

    // The hand when a click may move something: not while a booster opens in it or a drop is being aimed.
    private IHand? ClickingHand() =>
        _hand is { IsOpening: false } hand && !CardDropper.IsPreparingDrop ? hand : null;

    private void OnRightPress(MouseActionArgs args)
    {
        if (args.Pressed)
        {
            // The right button uses the front item: a container opens (a box swings its flaps, a booster box empties,
            // a booster opens into the hand) and a card starts aiming a drop. While a booster opens it does nothing.
            if (_hand == null || _hand.IsOpening)
                return;

            if (_hand.Front is CardContainer)
                _hand.UseFront();
            else
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
            card.CollisionLayer = _hand!.CardCollisionLayer; // Layer 2
        }
    }
}
