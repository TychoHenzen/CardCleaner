using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Player.Controllers;

/// <summary>
/// Generic interaction system that handles highlighting and interaction with any IInteractable object.
/// Replaces the card-specific interaction system with a more generic approach.
/// </summary>
public partial class InteractionSystem : Node3D
{
    private IInputService _inputService;
    private IInteractable _currentTarget;
    
    [Export] public Camera3D Camera { get; set; }
    [Export] public float RayLength { get; set; } = 100f;
    [Export] public uint InteractableCollisionMask { get; set; } = 4; // Layer 3 for interactables

    public override void _Ready()
    {
        ServiceLocator.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction("interact", MouseButton.Left, OnInteractPressed);
        });
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

        IInteractable newTarget = null;

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
        if (newTarget != _currentTarget)
        {
            // Clear previous target
            if (_currentTarget != null)
            {
                _currentTarget.ClearHighlight();
            }

            // Set new target
            _currentTarget = newTarget;

            // Highlight new target if valid
            if (_currentTarget?.CanInteract == true)
            {
                var distance = Camera.GlobalPosition.DistanceTo(_currentTarget.InteractionBody.GlobalPosition);
                if (distance <= _currentTarget.InteractionRange)
                {
                    _currentTarget.Highlight();
                }
                else
                {
                    _currentTarget = null; // Too far away
                }
            }
            else if (_currentTarget != null)
            {
                _currentTarget = null; // Can't interact
            }
        }
    }

    private void OnInteractPressed(bool pressed)
    {
        if (!pressed || _currentTarget?.CanInteract != true) return;

        var distance = Camera.GlobalPosition.DistanceTo(_currentTarget.InteractionBody.GlobalPosition);
        if (distance <= _currentTarget.InteractionRange)
        {
            _currentTarget.Interact();
        }
    }
}