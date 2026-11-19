using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Carries sealed containers in the player's hand: one cardboard box or booster box, or a stack of up to
///     <see cref="MostBoosters" /> booster packs. A held container is frozen, has no collision, and is a child of the
///     hand anchor. Q puts the top one back on the card spawner's node in front of the player, with its collision and
///     physics restored. Using it (<see cref="UseHeld" />, the right button) opens or closes a cardboard box's flaps,
///     empties a booster box into the hand as its eight booster packs (<see cref="CardContainer.EmptyIntoHand" />),
///     and opens the top booster pack in front of the player into the hand of cards
///     (<see cref="CardContainer.OpenInHand" />); the hand stays busy until those cards are in it.
/// </summary>
public partial class ContainerHolder : Node3D, IContainerHolder, IDependent
{
    [Signal]
    public delegate void ContainerPickedUpEventHandler(CardContainer container);

    [Signal]
    public delegate void ContainerDroppedEventHandler(CardContainer container);

    public const string GroupName = "container_holders";

    /// <summary>The most booster packs the hand holds at once: a booster box's worth.</summary>
    public const int MostBoosters = CardContainerLayout.ItemsPerContainer;

    private const float DefaultDropDistance = 1.5f;

    // How far away, and how level, the spot an item out of an open box is set down on may be: arm's reach, and a
    // surface no steeper than about 45 degrees. The item is set down a little above the spot and falls onto it.
    private const float PutDownReach = 4.5f;
    private const float LevelEnough = 0.7f;
    private const float PutDownLift = 0.05f;
    private const uint WorldLayer = 1;
    private static readonly Vector3 DefaultHoldOffset = new(0.5f, -0.4f, -1.2f);
    private static readonly Vector3 DefaultBoosterHoldOffset = new(-0.45f, -0.4f, -1.2f);

    // Each booster pack after the first is held a little nearer the player, higher and to the right, so the stack
    // shows: a booster pack is 0.045 m thick, so a 0.05 m step keeps them apart.
    private static readonly Vector3 BoosterStackStep = new(0.02f, 0.015f, 0.05f);

    [Export] public float DropDistance = DefaultDropDistance;
    [Export] public Vector3 HoldOffset = DefaultHoldOffset;

    /// <summary>
    ///     Where a booster is held, upright and facing the player: on the left, since a booster can be picked up with
    ///     cards in hand, and the cards fan out on the right (<see cref="CardHolder.FanCenter" />).
    /// </summary>
    [Export] public Vector3 BoosterHoldOffset = DefaultBoosterHoldOffset;

    // What the hand holds, bottom first; the last one is the one Q, the right button and stocking act on.
    private readonly List<HeldItem> _held = new();

    private IServiceResolver? _services;
    private IInputService? _inputService;
    private IControlHints? _hints;
    private Node3D? _handAnchor;
    private ICardHolder? _cards;
    private CardContainer? _opening;

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

    /// <summary>The top container in the hand, or null while nothing is held or a booster opens in it.</summary>
    public CardContainer? Held => IsOpening ? null : HeldItems().LastOrDefault()?.Container;

    public bool IsHolding => HeldItems().Count > 0 || IsOpening;
    public bool IsOpening => OpeningInHand() != null;

    /// <summary>How many containers the hand holds: one box, or up to <see cref="MostBoosters" /> booster packs.</summary>
    public int HeldCount => HeldItems().Count;

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

        var held = Held;
        if (held == null)
            _hints.Clear(this);
        else
            _hints.Publish(this,
                ContainerHints.For(held.Kind, held.IsLidOpen, held.Remaining, HeldCount, BoosterFitsInHand));
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
        if (IsOpening || !GodotObject.IsInstanceValid(container) || container.IsOpened || _handAnchor == null
            || !Takes(container))
            return false;

        // The layers are remembered before they are cleared, so a drop restores exactly what the container had.
        _held.Add(new HeldItem(container, container.CollisionLayer, container.CollisionMask));
        container.CollisionLayer = 0;
        container.CollisionMask = 0;
        container.Freeze = true;
        if (container.GetParent() != _handAnchor)
            container.Reparent(_handAnchor);
        container.Transform = PoseOf(container, _held.Count - 1);
        container.ClearHighlight();
        EmitSignal(SignalName.ContainerPickedUp, container);
        return true;
    }

    /// <summary>Sets the top held container down in front of the hand. False when nothing was released.</summary>
    public bool Drop()
    {
        if (Held == null || _handAnchor == null) return false;

        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return false;
        }

        var spawnerNode = _services.Get<ICardSpawner>().GetNode();
        var dropPosition = _handAnchor.GlobalPosition + (-_handAnchor.GlobalBasis.Z * DropDistance);
        var top = TakeTop();
        top.Container.Reparent(spawnerNode);
        top.Container.GlobalPosition = dropPosition;
        top.Container.CollisionLayer = top.Layer;
        top.Container.CollisionMask = top.Mask;
        top.Container.Freeze = false;
        EmitSignal(SignalName.ContainerDropped, top.Container);
        return true;
    }

    public bool PutOneDown()
    {
        if (Held is not { IsLidOpen: true, Storage: { Count: > 0 } storage } || _handAnchor is not Camera3D eye)
            return false;

        var from = eye.GlobalPosition;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from,
            To = from - eye.GlobalBasis.Z * PutDownReach,
            CollisionMask = WorldLayer,
            Exclude = CameraRay.OwnBodies(eye),
        });
        if (hit.Count == 0 || hit["normal"].AsVector3().Y < LevelEnough)
            return false;

        // Turned to the player's heading, so it lies square to the view.
        var heading = new Basis(Vector3.Up, eye.GlobalRotation.Y);
        return storage.TakeOnto(new Transform3D(heading, hit["position"].AsVector3() + Vector3.Up * PutDownLift));
    }

    public bool Discard()
    {
        if (Held == null)
            return false;

        var container = TakeTop().Container;
        container.GetParent()?.RemoveChild(container);
        container.QueueFree();
        return true;
    }

    public bool UseHeld()
    {
        if (Held is not { } container)
            return false;

        return container.Kind switch
        {
            CardContainerKind.Box => container.SetLidOpen(!container.IsLidOpen),
            CardContainerKind.Pack => EmptyIntoHand(container),
            _ => OpenInHand(container)
        };
    }

    // An empty hand takes any container; a hand of booster packs takes one more booster pack while it has room.
    private bool Takes(CardContainer container)
    {
        var held = HeldItems();
        if (held.Count == 0)
            return true;

        return container.Kind == CardContainerKind.Booster && held.Count < MostBoosters
               && held.All(item => item.Container.Kind == CardContainerKind.Booster && item.Container != container);
    }

    // A booster's cards all go into the hand, so it opens only while the hand has room for every one of them.
    private bool BoosterFitsInHand => _cards == null || _cards.Room >= CardContainerLayout.ItemsPerContainer;

    private bool OpenInHand(CardContainer booster)
    {
        if (_cards == null || !BoosterFitsInHand || !booster.OpenInHand(_cards))
            return false;

        // Opening marks the booster opened, so the hand lets go of it; the hand stays busy through _opening.
        _opening = booster;
        return true;
    }

    // The booster box leaves the hand, used up, and its booster packs take its place, held as picked-up ones are.
    private bool EmptyIntoHand(CardContainer pack)
    {
        var boosters = pack.EmptyIntoHand();
        foreach (var booster in boosters)
            TryPickUp(booster);

        return boosters.Count > 0;
    }

    // A booster pack is held upright with its front to the player, as one about to be torn open is, each one after
    // the first a step nearer; a box is carried level.
    private Transform3D PoseOf(CardContainer container, int index) =>
        container.Kind == CardContainerKind.Booster
            ? new Transform3D(CardFan.FacingCamera, BoosterHoldOffset + BoosterStackStep * index)
            : new Transform3D(Basis.Identity, HoldOffset);

    private HeldItem TakeTop()
    {
        var top = _held[^1];
        _held.RemoveAt(_held.Count - 1);
        return top;
    }

    // The booster opening in the hand, or null once its cards are in the hand or it was freed.
    private CardContainer? OpeningInHand()
    {
        if (_opening != null && (!GodotObject.IsInstanceValid(_opening) || !_opening.IsOpeningInHand))
            _opening = null;

        return _opening;
    }

    // What the hand still holds. A container that was freed, opened (a booster opening in the hand, an emptied booster
    // box) or taken off the hand anchor (a load removes it, a slot locks it) is let go here, and the ones left close
    // up their stack.
    private List<HeldItem> HeldItems()
    {
        if (_held.RemoveAll(item => !StillHeld(item.Container)) > 0)
        {
            for (var i = 0; i < _held.Count; i++)
                _held[i].Container.Transform = PoseOf(_held[i].Container, i);
        }

        return _held;
    }

    private bool StillHeld(CardContainer container) =>
        GodotObject.IsInstanceValid(container) && !container.IsOpened && container.GetParent() == _handAnchor;

    // One held container and the collision it had before the hand took it.
    private sealed record HeldItem(CardContainer Container, uint Layer, uint Mask);
}
