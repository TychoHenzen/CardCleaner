using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Components;

public partial class CardDropper : Node3D
{
    [Signal]
    public delegate void DropCancelledEventHandler();

    [Signal]
    public delegate void DropCompletedEventHandler();

    [Signal]
    public delegate void DropStartedEventHandler();

    private Camera3D _camera;
    private ICardHolder _cardHolder;
    private IDropPreview _dropPreview;
    private IInputService? _inputService;

    public bool IsPreparingDrop { get; private set; }

    public void Initialize(ICardHolder cardHolder, IDropPreview dropPreview, Camera3D camera)
    {
        _cardHolder = cardHolder;
        _dropPreview = dropPreview;
        _camera = camera;
        ServiceLocator.Get<IInputService>(input =>
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
        if (!_cardHolder.HasCards) return;

        IsPreparingDrop = true;
        _dropPreview.ShowPreview(true);
        _cardHolder.PositionCardsForDrop();

        EmitSignal(SignalName.DropStarted);
    }

    public void CancelDropPreparation()
    {
        if (!IsPreparingDrop) return;

        IsPreparingDrop = false;
        _dropPreview.ShowPreview(false);
        _cardHolder.PositionCards(); // Reset to normal hand position

        EmitSignal(SignalName.DropCancelled);
    }

    public void CompleteDropPreparation()
    {
        if (!IsPreparingDrop) return;

        _cardHolder.RemoveAllCards();
        IsPreparingDrop = false;
        _dropPreview.ShowPreview(false);

        EmitSignal(SignalName.DropCompleted);
    }

    public void DropSingleCard()
    {
        if (!_cardHolder.HasCards) return;
        _cardHolder.RemoveTopCard();
    }

    public void UpdateDropPreview()
    {
        if (!IsPreparingDrop || !_cardHolder.HasCards) return;

        // Update card positions to follow camera
        _cardHolder.PositionCardsForDrop();

        // Update preview ray
        var bottomCard = _cardHolder.HeldCards[0];
        var origin = bottomCard.GlobalTransform.Origin;
        var direction = Vector3.Down;

        _dropPreview.UpdatePreview(origin, direction);
    }
}