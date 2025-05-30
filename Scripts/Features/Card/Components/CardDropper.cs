using Godot;

public partial class CardDropper : Node3D
{
    [Signal] public delegate void DropStartedEventHandler();
    [Signal] public delegate void DropCancelledEventHandler();
    [Signal] public delegate void DropCompletedEventHandler();
    
    private bool _isPreparingDrop = false;
    private CardHolder _cardHolder;
    private DropPreview _dropPreview;
    private Camera3D _camera;

    public bool IsPreparingDrop => _isPreparingDrop;

    public void Initialize(CardHolder cardHolder, DropPreview dropPreview, Camera3D camera)
    {
        _cardHolder = cardHolder;
        _dropPreview = dropPreview;
        _camera = camera;
    }

    public void StartDropPreparation()
    {
        if (!_cardHolder.HasCards) return;
        
        _isPreparingDrop = true;
        _dropPreview.ShowPreview(true);
        _cardHolder.PositionCardsForDrop();
        
        EmitSignal(nameof(DropStarted));
    }

    public void CancelDropPreparation()
    {
        if (!_isPreparingDrop) return;
        
        _isPreparingDrop = false;
        _dropPreview.ShowPreview(false);
        _cardHolder.PositionCards(); // Reset to normal hand position
        
        EmitSignal(nameof(DropCancelled));
    }

    public void CompleteDropPreparation()
    {
        if (!_isPreparingDrop) return;
        
        _cardHolder.RemoveAllCards();
        _isPreparingDrop = false;
        _dropPreview.ShowPreview(false);
        
        EmitSignal(nameof(DropCompleted));
    }

    public void DropSingleCard()
    {
        if (!_cardHolder.HasCards) return;
        _cardHolder.RemoveTopCard();
    }

    public void UpdateDropPreview()
    {
        if (!_isPreparingDrop || !_cardHolder.HasCards) return;
    
        // Update card positions to follow camera
        _cardHolder.PositionCardsForDrop();
    
        // Update preview ray
        var bottomCard = _cardHolder.HeldCards[0];
        var origin = bottomCard.GlobalTransform.Origin;
        var direction = Vector3.Down;
    
        _dropPreview.UpdatePreview(origin, direction);
    }

}