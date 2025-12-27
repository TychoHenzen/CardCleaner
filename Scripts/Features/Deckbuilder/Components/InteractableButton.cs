using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Components;

/// <summary>
/// A physical button that can be clicked in the world.
/// Implements IInteractable for use with the generic interaction system.
/// </summary>
public partial class InteractableButton : StaticBody3D, IInteractable
{
    [Signal]
    public delegate void ButtonPressedEventHandler();

    // Default values for exported properties
    private const bool DefaultEnabled = true;
    private const float DefaultInteractionRange = 5.0f;
    private const float DefaultPressDepth = 0.02f;
    private const float DefaultPressAnimationSpeed = 0.1f;

    private Vector3 _originalPosition;
    private Tween? _pressTween;

    [Export] public bool Enabled { get; set; } = DefaultEnabled;
    [Export] public float PressDepth { get; set; } = DefaultPressDepth;
    [Export] public float PressAnimationSpeed { get; set; } = DefaultPressAnimationSpeed;
    [Export] public MeshInstance3D? ButtonMesh { get; set; }
    [Export] public MeshInstance3D? HighlightMesh { get; set; }
    [Export] public float InteractionRange { get; set; } = DefaultInteractionRange;

    public bool CanInteract => Enabled;
    public Node3D InteractionBody => this;

    public void Interact()
    {
        if (!CanInteract) return;

        ILog.Print($"Button '{Name}' pressed!");

        // Play press animation
        PlayPressAnimation();

        // Emit signal
        EmitSignal(SignalName.ButtonPressed);
    }

    public void Highlight()
    {
        if (!CanInteract) return; // Only highlight if we can interact

        if (HighlightMesh != null) HighlightMesh.Visible = true;
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null) HighlightMesh.Visible = false;
    }

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Enabled) => true,
            nameof(InteractionRange) => true,
            nameof(PressDepth) => true,
            nameof(PressAnimationSpeed) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(Enabled) => DefaultEnabled,
            nameof(InteractionRange) => DefaultInteractionRange,
            nameof(PressDepth) => DefaultPressDepth,
            nameof(PressAnimationSpeed) => DefaultPressAnimationSpeed,
            _ => base._PropertyGetRevert(property)
        };
    }

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
            ILog.Error($"{Name} needs a CollisionShape3D child for interaction detection");
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
        if (ButtonMesh != null)
            ButtonMesh.Visible = enabled;
        // Clear highlight if disabling
        if (!enabled) ClearHighlight();
    }
}
