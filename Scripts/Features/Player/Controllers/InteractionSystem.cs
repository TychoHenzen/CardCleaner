// Debug version of InteractionSystem with detailed logging
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Player.Controllers;

/// <summary>
/// Debug version of InteractionSystem with detailed logging to help identify issues
/// </summary>
public partial class InteractionSystem : Node3D
{
    private IInputService? _inputService;

    [Export] public Camera3D? Camera { get; set; }
    [Export] public float RayLength { get; set; } = 100f;
    [Export] public uint InteractableCollisionMask { get; set; } = 6; // Layer 2 (cards) + Layer 3 (buttons)
    public IInteractable? CurrentTarget { get; private set; }
    
    public override void _Ready()
    {
        GD.Print("[InteractionSystem] Starting up...");
        GD.Print($"[InteractionSystem] Camera: {Camera}");
        GD.Print($"[InteractionSystem] Collision Mask: {InteractableCollisionMask}");
        
        ServiceLocator.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "interact", MouseButton.Left, OnInteractPressed);
            GD.Print("[InteractionSystem] Registered input action");
        });
        
        // Check for existing cards
        var cards = GetTree().GetNodesInGroup("Cards");
        GD.Print($"[InteractionSystem] Found {cards.Count} existing cards");
        foreach (var card in cards)
        {
            GD.Print($"[InteractionSystem] Card: {card.Name}, CollisionLayer: {((RigidBody3D)card).CollisionLayer}");
        }
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        DetectInteractable();
    }

    private void DetectInteractable()
    {
        if (Camera == null)
            return;

        var origin = Camera.GlobalTransform.Origin;
        var forward = -Camera.GlobalTransform.Basis.Z;
        
        var result = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = origin,
            To = origin + forward * RayLength,
            CollideWithBodies = true,
            CollideWithAreas = false,
            CollisionMask = InteractableCollisionMask
        });

        IInteractable? newTarget = null;

        if (result.Count > 0 && result["collider"].Obj is Node3D body)
        {
            // Look for IInteractable on the body or its parents
            var current = body;
            while (current != null)
            {
                if (current is IInteractable interactable)
                {
                    newTarget = interactable;
                    break;
                }
                current = current.GetParent() as Node3D;
            }
        }

        // Handle target changes
        if (newTarget == CurrentTarget) 
            return;
        
        if (newTarget != null)
        {
            GD.Print($"[InteractionSystem] New target: {newTarget.InteractionBody.Name}");
        }
        else if (CurrentTarget != null)
        {
            GD.Print("[InteractionSystem] Lost target");
        }
        
        // Clear previous target
        CurrentTarget?.ClearHighlight();

        // Set new target
        CurrentTarget = newTarget;

        // Highlight new target if valid
        if (CurrentTarget?.CanInteract == true)
        {
            var distance = Camera.GlobalPosition.DistanceTo(CurrentTarget.InteractionBody.GlobalPosition);
            GD.Print($"[InteractionSystem] Target distance: {distance}, Range: {CurrentTarget.InteractionRange}");
            
            if (distance <= CurrentTarget.InteractionRange)
            {
                GD.Print("[InteractionSystem] Highlighting target");
                CurrentTarget.Highlight();
            }
            else
            {
                GD.Print("[InteractionSystem] Target too far away");
                CurrentTarget = null; // Too far away
            }
        }
        else
        {
            CurrentTarget = null; // Can't interact
        }
    }

    private void OnInteractPressed(bool pressed)
    {
        if (!pressed || CurrentTarget?.CanInteract != true || Camera == null) return;

        GD.Print($"[InteractionSystem] Interact pressed on: {CurrentTarget.InteractionBody.Name}");
        
        var distance = Camera.GlobalPosition.DistanceTo(CurrentTarget.InteractionBody.GlobalPosition);
        if (distance <= CurrentTarget.InteractionRange)
        {
            GD.Print("[InteractionSystem] Calling Interact()");
            CurrentTarget.Interact();
        }
        else
        {
            GD.Print($"[InteractionSystem] Too far to interact: {distance} > {CurrentTarget.InteractionRange}");
        }
    }
}