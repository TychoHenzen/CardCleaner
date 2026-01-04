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
    // Default values as constants
    private const float DefaultRayLength = 100f;
    private const uint DefaultInteractableCollisionMask = 6; // Layer 2 (cards) + Layer 3 (buttons)
    private const float RaycastInterval = 0.05f; // 20Hz = 50ms interval

    private IInputService? _inputService;
    private float _timeSinceLastRaycast = 0f;

    [Export] public Camera3D? Camera { get; set; }
    [Export] public float RayLength { get; set; } = DefaultRayLength;
    [Export] public uint InteractableCollisionMask { get; set; } = DefaultInteractableCollisionMask;
    private IInteractable? CurrentTarget { get; set; }

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(RayLength) => true,
            nameof(InteractableCollisionMask) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(RayLength) => DefaultRayLength,
            nameof(InteractableCollisionMask) => DefaultInteractableCollisionMask,
            _ => base._PropertyGetRevert(property)
        };
    }

    public override void _Ready()
    {
        if (ILog.ExportCheck(Camera, nameof(Camera), this))
            return;

        ILog.Print("Starting up...");
        ILog.Print("Starting up...");
        ILog.Print("Starting up...");
        ILog.Print($"Camera: {Camera}");
        ILog.Print($"Collision Mask: {InteractableCollisionMask}");

        ServiceLocator.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "interact", MouseButton.Left, OnInteractPressed);
            ILog.Print("Registered input action");
        });
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        _timeSinceLastRaycast += (float)delta;
        if (_timeSinceLastRaycast >= RaycastInterval)
        {
            DetectInteractable();
            _timeSinceLastRaycast -= RaycastInterval; // Preserve fractional remainder to prevent drift
        }
    }

    private void DetectInteractable()
    {
        if (Camera == null)
            return;

        var hitBody = PerformRaycast();
        var newTarget = hitBody != null ? FindInteractableInHierarchy(hitBody) : null;
        UpdateCurrentTarget(newTarget);
    }

    private Node3D? PerformRaycast()
    {
        if (Camera == null)
            return null;
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

        return result.Count > 0 && result["collider"].Obj is Node3D body ? body : null;
    }

    private static IInteractable? FindInteractableInHierarchy(Node3D startNode)
    {
        var current = startNode;
        while (current != null)
        {
            if (current is IInteractable interactable)
                return interactable;
            current = current.GetParent() as Node3D;
        }

        return null;
    }

    private void UpdateCurrentTarget(IInteractable? newTarget)
    {
        if (newTarget == CurrentTarget)
            return;

        // Clear previous target
        CurrentTarget?.ClearHighlight();

        // Set and validate new target
        CurrentTarget = newTarget;
        if (CurrentTarget?.CanInteract == true && IsTargetInRange(CurrentTarget))
        {
            ILog.Print("Highlighting target");
            CurrentTarget.Highlight();
        }
        else
        {
            CurrentTarget = null;
        }
    }

    private bool IsTargetInRange(IInteractable target)
    {
        if (Camera == null)
            return false;
        var distanceSquared = Camera.GlobalPosition.DistanceSquaredTo(target.InteractionBody.GlobalPosition);
        var rangeSquared = target.InteractionRange * target.InteractionRange;
        ILog.Print($"Target distance²: {distanceSquared}, Range²: {rangeSquared}");

        return distanceSquared <= rangeSquared;
    }

    private void OnInteractPressed(bool pressed)
    {
        if (!pressed || CurrentTarget?.CanInteract != true || Camera == null) return;

        ILog.Print($"Interact pressed on: {CurrentTarget.InteractionBody.Name}");

        var distanceSquared = Camera.GlobalPosition.DistanceSquaredTo(CurrentTarget.InteractionBody.GlobalPosition);
        var rangeSquared = CurrentTarget.InteractionRange * CurrentTarget.InteractionRange;
        if (distanceSquared <= rangeSquared)
        {
            ILog.Print("Calling Interact()");
            CurrentTarget.Interact();
        }
        else
        {
            ILog.Print($"Too far to interact: √{distanceSquared:F2} > {CurrentTarget.InteractionRange}");
        }
    }
}