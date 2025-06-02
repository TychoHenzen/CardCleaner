using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// A physical button that can be clicked in the world.
/// Implements IInteractable for use with the generic interaction system.
/// </summary>
[Tool]
public partial class InteractableButton : StaticBody3D, IInteractable
{
    [Signal]
    public delegate void ButtonPressedEventHandler();

    private Vector3 _originalPosition;
    private Tween? _pressTween;
    
    [Export] public bool Enabled { get; set; } = true;
    [Export] public float InteractionRange { get; set; } = 5.0f;
    [Export] public float PressDepth { get; set; } = 0.02f;
    [Export] public float PressAnimationSpeed { get; set; } = 0.1f;
    [Export] public MeshInstance3D? ButtonMesh { get; set; }
    [Export] public MeshInstance3D? HighlightMesh { get; set; }

    public bool CanInteract => Enabled;
    public Node3D InteractionBody => this;

    public override void _Ready()
    {
        // Set up collision layer for interactables (layer 3)
        CollisionLayer = 4; // Layer 3 (bit 2^2 = 4)

        if (ButtonMesh != null)
        {
            _originalPosition = ButtonMesh.Position;
            ButtonMesh.Visible = false;
        }
        
        // Hide highlight initially
        if (HighlightMesh != null) HighlightMesh.Visible = false;
        
        // Ensure we have a collision shape
        if (GetChildren().OfType<CollisionShape3D>().FirstOrDefault() == null)
        {
            ILog.Error($"{Name} needs a CollisionShape3D child for interaction detection");
        }
    }

    public void Interact()
    {
        if (!CanInteract) return;
        
        ILog.Print($"Button '{Name}' pressed!");
        
        // Play press animation
        PlayPressAnimation();
        
        // Emit signal
        EmitSignal(nameof(ButtonPressed));
    }

    public void Highlight()
    {
        if (!CanInteract) return; // Only highlight if we can interact
        
        if (HighlightMesh != null)
        {
            HighlightMesh.Visible = true;
        }
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null)
        {
            HighlightMesh.Visible = false;
        }
    }

    private void PlayPressAnimation()
    {
        if (ButtonMesh == null) return;
        
        // Kill existing tween
        _pressTween?.Kill();
        _pressTween = CreateTween();
        
        // Press down
        var pressedPosition = _originalPosition + Vector3.Down * PressDepth;
        _pressTween.TweenProperty(ButtonMesh, "position", pressedPosition, PressAnimationSpeed);
        
        // Return to original position
        _pressTween.TweenProperty(ButtonMesh, "position", _originalPosition, PressAnimationSpeed);
    }

    /// <summary>
    /// Enable or disable the button programmatically
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        if(ButtonMesh != null)
            ButtonMesh.Visible = enabled;
        // Clear highlight if disabling
        if (!enabled)
        {
            ClearHighlight();
        }
    }
}