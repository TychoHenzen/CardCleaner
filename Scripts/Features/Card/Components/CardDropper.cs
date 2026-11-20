using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Components;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

/// <summary>
///     Lets go of held cards: X drops the front card where it hangs, and a drop aimed with the right button lays every
///     held card flat in front of the player and lets go of them all on release. Booster packs held beside the cards
///     stay in the hand.
/// </summary>
public partial class CardDropper : Node3D, IDependent
{
    [Signal]
    public delegate void DropCancelledEventHandler();

    [Signal]
    public delegate void DropCompletedEventHandler();

    [Signal]
    public delegate void DropStartedEventHandler();

    private IHand _hand = null!;
    private IDropPreview _dropPreview = null!;
    private IInputService? _inputService;
    private IServiceResolver? _services;

    public bool IsPreparingDrop { get; private set; }

    public void Inject(IServiceResolver services) => _services = services;

    public void Initialize(IHand hand, IDropPreview dropPreview)
    {
        _hand = hand;
        _dropPreview = dropPreview;
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "card_drop_single", Key.X, DropSingleCard);
        });
    }

    public override void _ExitTree()
    {
        // Clean up input registrations when component is destroyed
        _inputService?.UnregisterAllActions(this);
    }

    public void StartDropPreparation()
    {
        if (_hand.Cards.Count == 0 || _hand.IsOpening) return;

        IsPreparingDrop = true;
        _dropPreview.ShowPreview(true);
        _hand.PositionCardsForDrop();

        EmitSignal(SignalName.DropStarted);
    }

    public void CancelDropPreparation()
    {
        if (!IsPreparingDrop) return;

        IsPreparingDrop = false;
        _dropPreview.ShowPreview(false);
        _hand.PositionItems(); // Reset to normal hand position

        EmitSignal(SignalName.DropCancelled);
    }

    public void CompleteDropPreparation()
    {
        if (!IsPreparingDrop) return;

        // The last card first, as the old holder let go of them, so each release re-lays the rest only once.
        var cards = _hand.Cards;
        for (var i = cards.Count - 1; i >= 0; i--)
            _hand.ReleaseCard(cards[i]);
        IsPreparingDrop = false;
        _dropPreview.ShowPreview(false);
        _hand.PositionItems();

        EmitSignal(SignalName.DropCompleted);
    }

    /// <summary>X: lets go of the front item where it hangs when it is a card; a container in front stays.</summary>
    public void DropSingleCard()
    {
        if (IsPreparingDrop || _hand.Front is not CardController card) return;
        _hand.ReleaseCard(card);
    }

    public void UpdateDropPreview()
    {
        var cards = _hand.Cards;
        if (!IsPreparingDrop || cards.Count == 0) return;

        // Update card positions to follow camera
        _hand.PositionCardsForDrop();

        // Update preview ray
        _dropPreview.UpdatePreview(cards[0].GlobalTransform.Origin, Vector3.Down);
    }
}
