using CardCleaner.Scripts.Core.Interfaces;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Carries one sealed container in the player's hand. A held container is frozen, has no collision, and is
///     a child of the hand anchor. Dropping puts it back on the card spawner's node in front of the player,
///     with its collision and physics restored. Opening it drops it first, then opens it where it lies.
/// </summary>
public partial class ContainerHolder : Node3D, IContainerHolder, IDependent
{
    [Signal]
    public delegate void ContainerPickedUpEventHandler(CardContainer container);

    [Signal]
    public delegate void ContainerDroppedEventHandler(CardContainer container);

    public const string GroupName = "container_holders";

    private const float DefaultDropDistance = 1.5f;
    private static readonly Vector3 DefaultHoldOffset = new(0.5f, -0.4f, -1.2f);

    [Export] public float DropDistance = DefaultDropDistance;
    [Export] public Vector3 HoldOffset = DefaultHoldOffset;

    private IServiceResolver? _services;
    private IInputService? _inputService;
    private Node3D? _handAnchor;
    private CardContainer? _held;
    private uint _savedLayer;
    private uint _savedMask;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(DropDistance) => true,
            nameof(HoldOffset) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(DropDistance) => DefaultDropDistance,
            nameof(HoldOffset) => DefaultHoldOffset,
            _ => base._PropertyGetRevert(property)
        };
    }

    public CardContainer? Held => HeldContainer();
    public bool IsHolding => HeldContainer() != null;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _EnterTree() => AddToGroup(GroupName);

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IInputService>(input =>
        {
            _inputService = input;
            _inputService.RegisterAction(this, "container_open", Key.Q, () => OpenHeld());
        });
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);
    }

    public void SetReferences(Node3D handAnchor)
    {
        _handAnchor = handAnchor;
    }

    public bool TryPickUp(CardContainer container)
    {
        if (HeldContainer() != null || !GodotObject.IsInstanceValid(container) || container.IsOpened
            || _handAnchor == null)
            return false;

        // Remember the layers before clearing them, so a drop restores exactly what the container had.
        _savedLayer = container.CollisionLayer;
        _savedMask = container.CollisionMask;
        container.CollisionLayer = 0;
        container.CollisionMask = 0;
        container.Freeze = true;
        container.Reparent(_handAnchor);
        container.Transform = new Transform3D(Basis.Identity, HoldOffset);
        container.ClearHighlight();

        // Subscribed after the reparent, which leaves the tree and would otherwise clear the held container at once.
        container.TreeExiting += OnHeldTreeExiting;
        _held = container;
        EmitSignal(SignalName.ContainerPickedUp, container);
        return true;
    }

    public void Drop()
    {
        var container = HeldContainer();
        if (container == null || _handAnchor == null) return;

        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        var spawnerNode = _services.Get<ICardSpawner>().GetNode();
        var dropPosition = _handAnchor.GlobalPosition + (-_handAnchor.GlobalBasis.Z * DropDistance);

        // Unsubscribed before the reparent, which leaves the tree and would otherwise clear the held container.
        container.TreeExiting -= OnHeldTreeExiting;
        container.Reparent(spawnerNode);
        container.GlobalPosition = dropPosition;
        container.CollisionLayer = _savedLayer;
        container.CollisionMask = _savedMask;
        container.Freeze = false;
        _held = null;
        EmitSignal(SignalName.ContainerDropped, container);
    }

    public bool OpenHeld()
    {
        var container = HeldContainer();
        if (container == null) return false;

        Drop();
        return container.Open();
    }

    // The held container was freed while the hand held it, so the hand lets go of it.
    private void OnHeldTreeExiting()
    {
        _held = null;
    }

    // The container the hand holds, or null. A container that was freed or opened is no longer held, so the hand
    // lets go of it here and unsubscribes from it, so a later held container is never cleared by a stale handler.
    private CardContainer? HeldContainer()
    {
        if (_held == null) return null;
        if (GodotObject.IsInstanceValid(_held) && !_held.IsOpened) return _held;

        if (GodotObject.IsInstanceValid(_held))
            _held.TreeExiting -= OnHeldTreeExiting;
        _held = null;
        return null;
    }
}
