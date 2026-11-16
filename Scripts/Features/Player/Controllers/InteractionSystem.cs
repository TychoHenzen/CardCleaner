using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using Godot;

namespace CardCleaner.Scripts.Features.Player.Controllers;

/// <summary>
///     Finds the interactable on the camera's centre ray, highlights it, and calls its Interact on a left click.
///     The target is judged again on every scan, so one that goes out of range or stops accepting interaction is
///     dropped at once: the prompt under the crosshair never offers a click that would do nothing.
/// </summary>
public partial class InteractionSystem : Node3D, IDependent
{
    private const float DefaultRayLength = 100f;
    private const uint DefaultInteractableCollisionMask = 6; // Layer 2 (cards) + Layer 3 (buttons)

    /// <summary>Seconds between target scans (20 Hz). A click only reaches what the last scan picked.</summary>
    public const float RaycastInterval = 0.05f;

    private IInputService? _inputService;
    private IServiceResolver? _services;
    private float _timeSinceLastRaycast;

    [Export] public Camera3D? Camera { get; set; }
    [Export] public float RayLength { get; set; } = DefaultRayLength;
    [Export] public uint InteractableCollisionMask { get; set; } = DefaultInteractableCollisionMask;

    private IInteractable? _target;

    /// <summary>
    ///     The interactable a click would use now, or null. A target freed since the last scan reads as null at once,
    ///     because the HUD reads it every frame.
    /// </summary>
    public IInteractable? Target => _target != null && IsAlive(_target) ? _target : null;

    public void Inject(IServiceResolver services) => _services = services;

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
        if (NodeExports.ExportCheck(Camera, nameof(Camera), this))
            return;

        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "interact", MouseButton.Left, OnInteractPressed);
        });
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        _timeSinceLastRaycast += (float)delta;
        if (_timeSinceLastRaycast < RaycastInterval)
            return;

        DetectInteractable();
        _timeSinceLastRaycast -= RaycastInterval; // Preserve fractional remainder to prevent drift
    }

    private void DetectInteractable()
    {
        var hitBody = PerformRaycast();
        var candidate = hitBody != null ? FindInteractableInHierarchy(hitBody) : null;
        UpdateTarget(candidate != null && IsUsable(candidate) ? candidate : null);
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

    private void UpdateTarget(IInteractable? candidate)
    {
        var current = Target;
        if (candidate == current)
        {
            _target = current;
            return;
        }

        current?.ClearHighlight();
        _target = candidate;
        _target?.Highlight();
    }

    // A freed node (an opened box, a sold card) can still be the last target; it is never touched again.
    private static bool IsAlive(IInteractable target) => target is not GodotObject node || IsInstanceValid(node);

    private bool IsUsable(IInteractable target) => IsAlive(target) && target.CanInteract && IsTargetInRange(target);

    private bool IsTargetInRange(IInteractable target)
    {
        if (Camera == null)
            return false;
        var distanceSquared = Camera.GlobalPosition.DistanceSquaredTo(target.InteractionBody.GlobalPosition);
        return distanceSquared <= target.InteractionRange * target.InteractionRange;
    }

    private void OnInteractPressed(bool pressed)
    {
        // Judged again at the click: the target can change between scans.
        if (pressed && Target is { } target && IsUsable(target))
            target.Interact();
    }
}
