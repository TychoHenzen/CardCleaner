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
    private IInteractable? CurrentTarget { get; set; }
    
    public override void _Ready()
    {
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
        DetectInteractable();
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
    var distance = Camera.GlobalPosition.DistanceTo(target.InteractionBody.GlobalPosition);
    ILog.Print($"Target distance: {distance}, Range: {target.InteractionRange}");
    
    return distance <= target.InteractionRange;

}
    private void OnInteractPressed(bool pressed)
    {
        if (!pressed || CurrentTarget?.CanInteract != true || Camera == null) return;

        ILog.Print($"Interact pressed on: {CurrentTarget.InteractionBody.Name}");
        
        var distance = Camera.GlobalPosition.DistanceTo(CurrentTarget.InteractionBody.GlobalPosition);
        if (distance <= CurrentTarget.InteractionRange)
        {
            ILog.Print("Calling Interact()");
            CurrentTarget.Interact();
        }
        else
        {
            ILog.Print($"Too far to interact: {distance} > {CurrentTarget.InteractionRange}");
        }
    }
}