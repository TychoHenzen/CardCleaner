using CardCleaner.Scripts.Features.Card.Components;
using Godot;

public partial class CardHighlighter : Node3D
{
    [Export] public Camera3D Camera;
    [Export] public Node3D CardsParent;
    [Export] public float MaxHighlightDistance = 50f;
    
    [Export] public InputHandler Input;
    [Export] public CardPicker Picker;
    [Export] public DropPreview Preview;
    [Export] public CardHolder CardHolder;
    [Export] public CardDropper CardDropper;
    private RigidBody3D _lastCard;

    public override void _Ready()
    {
        // Get references
        CardHolder.SetReferences(Camera, CardsParent);
        CardDropper.Initialize(CardHolder, Preview, Camera);

        // Wire signals
        Input.Connect("RightPress", Callable.From(OnRightPress));
        Input.Connect("RightRelease", Callable.From(OnRightRelease));
        Input.Connect("LeftPress", Callable.From(OnLeftPress));
        Input.Connect("KeyPressed", Callable.From<InputEventKey>(OnKeyPressed));
        
        Picker.Connect("CardDetected", Callable.From<RigidBody3D>(OnCardDetected));
        Picker.Connect("NoCardDetected", Callable.From(OnNoCardDetected));
        
        SetupCardCollisionLayers();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        // Update drop preview while preparing drop
        if (CardDropper.IsPreparingDrop)
        {
            CardDropper.UpdateDropPreview();
        }
    }

    private void OnRightPress()
    {
        CardDropper.StartDropPreparation();
    }

    private void OnRightRelease()
    {
        CardDropper.CompleteDropPreparation();
    }

    private void OnLeftPress()
    {
        if (CardDropper.IsPreparingDrop)
        {
            CardDropper.CancelDropPreparation();
        }
        else if (_lastCard != null && Camera.GlobalPosition.DistanceTo(_lastCard.GlobalPosition) <= MaxHighlightDistance)
        {
            CardHolder.AddCard(_lastCard);
            ClearHighlight(_lastCard);
        }
    }

    private void OnKeyPressed(InputEventKey keyEvent)
    {
        if (CardDropper.IsPreparingDrop && keyEvent.Keycode == Key.X)
        {
            CardDropper.DropSingleCard();
        }
    }

    private void OnCardDetected(RigidBody3D card)
    {
        if (CardDropper.IsPreparingDrop) return; // Don't highlight during drop prep
        
        if (_lastCard == card) return;
        ClearHighlight(_lastCard);
        if(card != null)
            HighlightCard(card);
        _lastCard = card;
    }

    private void OnNoCardDetected()
    {
        if (CardDropper.IsPreparingDrop) return; // Don't change highlights during drop prep
        
        ClearHighlight(_lastCard);
        _lastCard = null;
    }
    
    private void HighlightCard(RigidBody3D card)
    {
        if (Camera.GlobalPosition.DistanceTo(card.GlobalPosition) > MaxHighlightDistance) return;
        
        var outline = card.GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline == null) return;
        outline.Visible = true;
        _lastCard = card;
    }

    private void ClearHighlight(RigidBody3D card)
    {
        var outline = card?.GetNodeOrNull<CsgBox3D>("OutlineBox");
        if (outline == null) return;
        outline.Visible = false;
        _lastCard = null;
    }

    private void SetupCardCollisionLayers()
    {
        var cards = GetTree().GetNodesInGroup("Cards");
        foreach (RigidBody3D card in cards)
        {
            card.CollisionLayer = CardHolder.CardCollisionLayer;
        }
    }
}