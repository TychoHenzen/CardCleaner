using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Carries one sealed container in the player's hand. A held container is frozen, has no collision, and is
///     a child of the hand anchor. Q puts it back on the card spawner's node in front of the player, with its
///     collision and physics restored. Using it (<see cref="UseHeld" />, the right button) opens or closes a box's
///     flaps or a pack's lid in the hand, and opens a booster in front of the player into the hand of cards
///     (<see cref="CardContainer.OpenInHand" />); the hand stays busy until those cards are in it.
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
    private static readonly Vector3 DefaultBoosterHoldOffset = new(-0.45f, -0.4f, -1.2f);

    [Export] public float DropDistance = DefaultDropDistance;
    [Export] public Vector3 HoldOffset = DefaultHoldOffset;

    /// <summary>
    ///     Where a booster is held, upright and facing the player: on the left, since a booster can be picked up with
    ///     cards in hand, and the cards fan out on the right (<see cref="CardHolder.FanCenter" />).
    /// </summary>
    [Export] public Vector3 BoosterHoldOffset = DefaultBoosterHoldOffset;

    private IServiceResolver? _services;
    private IInputService? _inputService;
    private IControlHints? _hints;
    private Node3D? _handAnchor;
    private ICardHolder? _cards;
    private CardContainer? _held;
    private CardContainer? _opening;
    private uint _savedLayer;
    private uint _savedMask;

    public override bool _PropertyCanRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(DropDistance) => true,
            nameof(HoldOffset) => true,
            nameof(BoosterHoldOffset) => true,
            _ => base._PropertyCanRevert(property)
        };
    }

    public override Variant _PropertyGetRevert(StringName property)
    {
        return property.ToString() switch
        {
            nameof(DropDistance) => DefaultDropDistance,
            nameof(HoldOffset) => DefaultHoldOffset,
            nameof(BoosterHoldOffset) => DefaultBoosterHoldOffset,
            _ => base._PropertyGetRevert(property)
        };
    }

    public CardContainer? Held => HeldContainer();
    public bool IsHolding => HeldContainer() != null || IsOpening;
    public bool IsOpening => OpeningInHand() != null;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _EnterTree()
    {
        AddToGroup(GroupName);
        SetPhysicsProcess(true);
    }

    // The board drops repeated identical hints, so publishing every frame costs one comparison.
    public override void _PhysicsProcess(double delta)
    {
        if (_hints == null)
            return;

        var held = HeldContainer();
        if (held == null)
            _hints.Clear(this);
        else
            _hints.Publish(this, ContainerHints.For(held.Kind, held.IsLidOpen));
    }

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
            _inputService.RegisterAction(this, "container_put_down", Key.Q, () => Drop());
        });
        _services.Get<IControlHints>(hints => _hints = hints);
    }

    public override void _ExitTree()
    {
        _inputService?.UnregisterAllActions(this);
        _hints?.Clear(this);
    }

    public void SetReferences(Node3D handAnchor, ICardHolder? cards = null)
    {
        _handAnchor = handAnchor;
        _cards = cards;
    }

    public bool TryPickUp(CardContainer container)
    {
        if (IsHolding || !GodotObject.IsInstanceValid(container) || container.IsOpened || _handAnchor == null)
            return false;

        // Remember the layers before clearing them, so a drop restores exactly what the container had.
        _savedLayer = container.CollisionLayer;
        _savedMask = container.CollisionMask;
        container.CollisionLayer = 0;
        container.CollisionMask = 0;
        container.Freeze = true;
        container.Reparent(_handAnchor);
        // A booster is held upright with its front to the player, as a pack about to be torn open is; a box or pack is
        // carried level.
        container.Transform = container.Kind == CardContainerKind.Booster
            ? new Transform3D(CardFan.FacingCamera, BoosterHoldOffset)
            : new Transform3D(Basis.Identity, HoldOffset);
        container.ClearHighlight();

        // Subscribed after the reparent, which leaves the tree and would otherwise clear the held container at once.
        container.TreeExiting += OnHeldTreeExiting;
        _held = container;
        EmitSignal(SignalName.ContainerPickedUp, container);
        return true;
    }

    /// <summary>Sets the held container down in front of the hand. False when nothing was released.</summary>
    public bool Drop()
    {
        var container = HeldContainer();
        if (container == null || _handAnchor == null) return false;

        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return false;
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
        return true;
    }

    public bool UseHeld()
    {
        var container = HeldContainer();
        if (container == null) return false;

        if (container.HasLid)
            return container.SetLidOpen(!container.IsLidOpen);

        if (_cards == null || !container.OpenInHand(_cards))
            return false;

        // Opening marks the booster opened, so HeldContainer lets go of it; the hand stays busy through _opening.
        _opening = container;
        return true;
    }

    // The booster opening in the hand, or null once its cards are in the hand or it was freed.
    private CardContainer? OpeningInHand()
    {
        if (_opening != null && (!GodotObject.IsInstanceValid(_opening) || !_opening.IsOpeningInHand))
            _opening = null;

        return _opening;
    }

    // The held container was freed while the hand held it, so the hand lets go of it.
    private void OnHeldTreeExiting()
    {
        _held = null;
    }

    // The container the hand holds, or null. A container that was freed or opened (an emptied box counts as opened)
    // is no longer held, so the hand lets go of it here and unsubscribes from it, so a later held container is never
    // cleared by a stale handler.
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
