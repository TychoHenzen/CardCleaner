using System;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Utilities;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Hand.Components;

/// <summary>
///     The player's one hand (<see cref="IHand" />): the live bodies of the held cards and sealed containers, kept in
///     order by <see cref="HandItems{T}" />. Cards and booster packs fan out in front of the camera
///     (<see cref="CardFan" />) with the front item raised out of the fan; a booster box or a cardboard box is carried
///     alone, level. The wheel moves the front, Q sets the front item down in front of the player, and
///     <see cref="UseFront" /> (the right button on a container) opens it. While a booster opens in the hand
///     (<see cref="CardContainer.OpenInHand" />) the hand takes, gives and uses nothing.
/// </summary>
public partial class Hand : Node3D, IHand, IDependent
{
    private const uint DefaultCardCollisionLayer = 2;
    private const float DefaultHoldDistance = 1f;
    private const float DefaultFanRadius = 0.8f;
    private const float DefaultFanSpreadDegrees = 50f;
    private const int DefaultFanFillCount = 10;
    private const float DefaultFanDepthStep = 0.006f;
    private const float DefaultFrontRaise = 0.1f;
    private const float DefaultPutDownDistance = 1.5f;
    private static readonly Vector3 DefaultFanCenter = new(0.5f, -0.55f, -1.4f);
    private static readonly Vector3 DefaultBoxHoldOffset = new(0.5f, -0.4f, -1.2f);

    // How far away, and how level, the spot an item out of an open box is set down on may be: arm's reach, and a
    // surface no steeper than about 45 degrees. The item is set down a little above the spot and falls onto it.
    private const float PutOneDownReach = 4.5f;
    private const float LevelEnough = 0.7f;
    private const float PutDownLift = 0.05f;
    private const uint WorldLayer = 1;

    [Export] public uint CardCollisionLayerExport = DefaultCardCollisionLayer;

    /// <summary>How far in front of the hand anchor the cards lie while a drop is being aimed.</summary>
    [Export] public float HoldDistance = DefaultHoldDistance;

    /// <summary>Where the middle of the held fan sits, in the hand anchor's frame.</summary>
    [Export] public Vector3 FanCenter = DefaultFanCenter;

    /// <summary>Distance from the point the fan turns about (straight below the middle item) to each item.</summary>
    [Export] public float FanRadius = DefaultFanRadius;

    /// <summary>Angle between the outermost items once the hand holds <see cref="FanFillCount" /> or more.</summary>
    [Export] public float FanSpreadDegrees = DefaultFanSpreadDegrees;

    /// <summary>How many items it takes to fill the spread (see <see cref="HandFan" />).</summary>
    [Export] public int FanFillCount = DefaultFanFillCount;

    /// <summary>How much nearer the camera each next item lies, so they never sink into each other.</summary>
    [Export] public float FanDepthStep = DefaultFanDepthStep;

    /// <summary>How far the front item stands out of the fan, along its own lean.</summary>
    [Export] public float FrontRaise = DefaultFrontRaise;

    /// <summary>How far in front of the camera Q sets the front item down.</summary>
    [Export] public float PutDownDistance = DefaultPutDownDistance;

    /// <summary>Where a booster box or a cardboard box is carried, level, in the hand anchor's frame.</summary>
    [Export] public Vector3 BoxHoldOffset = DefaultBoxHoldOffset;

    private readonly HandItems<Node3D> _items = new(IsExclusive);

    // The collision a container had before the hand took it, so setting it down restores exactly that.
    private readonly Dictionary<Node3D, (uint Layer, uint Mask)> _collision = new();

    private IServiceResolver? _services;
    private IInputService? _input;
    private Node3D? _anchor;
    private CardContainer? _opening;

    // True while the cards lie in the drop layout (PositionCardsForDrop) rather than the fan.
    private bool _laidForDrop;

    public override bool _PropertyCanRevert(StringName property) =>
        property.ToString() switch
        {
            nameof(CardCollisionLayerExport) or nameof(HoldDistance) or nameof(FanCenter) or nameof(FanRadius)
                or nameof(FanSpreadDegrees) or nameof(FanFillCount) or nameof(FanDepthStep) or nameof(FrontRaise)
                or nameof(PutDownDistance) or nameof(BoxHoldOffset) => true,
            _ => base._PropertyCanRevert(property)
        };

    public override Variant _PropertyGetRevert(StringName property) =>
        property.ToString() switch
        {
            nameof(CardCollisionLayerExport) => DefaultCardCollisionLayer,
            nameof(HoldDistance) => DefaultHoldDistance,
            nameof(FanCenter) => DefaultFanCenter,
            nameof(FanRadius) => DefaultFanRadius,
            nameof(FanSpreadDegrees) => DefaultFanSpreadDegrees,
            nameof(FanFillCount) => DefaultFanFillCount,
            nameof(FanDepthStep) => DefaultFanDepthStep,
            nameof(FrontRaise) => DefaultFrontRaise,
            nameof(PutDownDistance) => DefaultPutDownDistance,
            nameof(BoxHoldOffset) => DefaultBoxHoldOffset,
            _ => base._PropertyGetRevert(property)
        };

    public IReadOnlyList<Node3D> Items => Live().Items;
    public int Count => Live().Count;
    public int FrontIndex => Live().FrontIndex;
    public Node3D? Front => Live().Front;
    public int Room => IsOpening ? 0 : Live().Room;
    public IReadOnlyList<RigidBody3D> Cards => Live().Items.OfType<CardController>().Cast<RigidBody3D>().ToList();
    public bool IsOpening => OpeningInHand() != null;
    public uint CardCollisionLayer => CardCollisionLayerExport;

    private CardContainer? FrontContainer => Front as CardContainer;

    public bool FrontBoosterFits => Live().RoomOnceFrontLeaves >= CardContainerLayout.ItemsPerContainer;

    public void Inject(IServiceResolver services) => _services = services;

    public override void _EnterTree() => AddToGroup(IHand.GroupName);

    public override void _Ready()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        // A wheel step arrives as a press and then a release; the front moves on the press only.
        _services.Get<IInputService>(input =>
        {
            _input = input;
            _input.RegisterAction(this, "hand_next", MouseButton.WheelUp, args => OnWheel(args, 1));
            _input.RegisterAction(this, "hand_previous", MouseButton.WheelDown, args => OnWheel(args, -1));
            _input.RegisterAction(this, "hand_put_down", Key.Q, () => PutFrontDown());
        });
    }

    public override void _ExitTree() => _input?.UnregisterAllActions(this);

    public void SetReferences(Node3D handAnchor) => _anchor = handAnchor;

    public bool CanTake(Node3D body) =>
        !IsOpening && _anchor != null && GodotObject.IsInstanceValid(body) && !Live().Items.Contains(body)
        && body switch
        {
            CardController => true,
            CardContainer container => !container.IsOpened,
            _ => false
        }
        && _items.CanAdd(body);

    public bool TryTake(Node3D body)
    {
        if (!CanTake(body) || body is not RigidBody3D rigid)
            return false;

        // A held body is out of play: frozen, and on no collision layer, so no slot trigger it still overlaps takes it
        // back (DeckSlot.IsFreeStock). A card also turns its shape off; a container keeps its layers for later.
        if (body is CardContainer container)
        {
            _collision[body] = (container.CollisionLayer, container.CollisionMask);
            container.CollisionMask = 0;
            container.ClearHighlight();
        }
        else if (rigid.GetNodeOrNull<CollisionShape3D>("CardCollision") is { } shape)
        {
            shape.Disabled = true;
        }

        rigid.CollisionLayer = 0;
        rigid.Freeze = true;
        if (body.GetParent() != _anchor)
            body.Reparent(_anchor);
        _items.Add(body);

        if (body is CardController card)
            card.EmitPickupSignal();

        PositionItems();
        return true;
    }

    public bool HandOver(Node3D body)
    {
        if (IsOpening || !Live().Items.Contains(body) || SpawnerNode() is not { } world)
            return false;

        Let(body);
        // Into the world before its collision comes back: a collidable body reparented out of the camera re-enters
        // the physics space at its hand pose, inside the player's capsule, and the player's next move is pushed out
        // of it (a stocked booster shoved the player 0.36 m toward the shelf).
        body.Reparent(world);
        RestoreCollision(body);
        PositionItems();
        return true;
    }

    // A look at the front item must not draw a container's seed, so Peek reads the record as it stands.
    public StoredItem? Peek() =>
        !IsOpening && Front is RigidBody3D front && StoredItemBodies.ShapeOf(front) != null
            ? StoredItemBodies.CurrentRecordOf(front)
            : null;

    // The same rule HandItems applies to bodies: a booster box or cardboard box only into an empty hand, anything else
    // while there is room (none while such a box is held).
    public bool CanPut(StoredItem item) =>
        !IsOpening && _anchor != null
                   && item switch
                   {
                       StoredContainer { Kind: CardContainerKind.Box or CardContainerKind.Pack } => Count == 0,
                       StoredCard or StoredContainer => Room > 0,
                       _ => false
                   };

    public int RoomFor(StoredItem item) =>
        !CanPut(item) ? 0
        : item is StoredContainer { Kind: CardContainerKind.Box or CardContainerKind.Pack } ? 1
        : Room;

    public bool Put(StoredItem item)
    {
        if (!CanPut(item) || _anchor == null || _services == null || SpawnerNode() is not Node3D world)
            return false;

        // Built at the anchor and taken in before any physics step, so it never collides with the player.
        var spawning = _services.Has<ICardSpawningService>() ? _services.Get<ICardSpawningService>() : null;
        if (StoredItemBodies.Build(item, world, _anchor.GlobalTransform, spawning) is not { } body)
            return false;
        if (TryTake(body))
            return true;

        body.GetParent()?.RemoveChild(body);
        body.QueueFree();
        return false;
    }

    // The front body leaves as its record (a container without a seed draws one now) and is freed.
    public StoredItem? TakeLast()
    {
        if (IsOpening || Front is not RigidBody3D front || StoredItemBodies.ShapeOf(front) == null)
            return null;

        var record = StoredItemBodies.RecordOf(front);
        Let(front);
        front.GetParent()?.RemoveChild(front);
        front.QueueFree();
        PositionItems();
        return record;
    }

    public bool PutFrontDown()
    {
        if (IsOpening || Front is not RigidBody3D front || _anchor == null || SpawnerNode() is not { } world)
            return false;

        var at = _anchor.GlobalPosition - _anchor.GlobalBasis.Z * PutDownDistance;
        Let(front);
        front.Reparent(world);
        front.GlobalPosition = at;
        RestoreCollision(front);
        front.Freeze = false;
        PositionItems();
        return true;
    }

    public bool ReleaseCard(RigidBody3D card)
    {
        if (IsOpening || card is not CardController || !Live().Items.Contains(card)
            || SpawnerNode() is not { } world)
            return false;

        Let(card);
        // Reparented first, then physics comes back a frame later, so the card does not meet the hand's other cards.
        card.Reparent(world);
        CallDeferred(MethodName.EnablePhysicsDeferred, card);
        if (_laidForDrop)
            PositionCardsForDrop();
        else
            PositionItems();
        return true;
    }

    public void Rotate(int places)
    {
        if (IsOpening)
            return;
        Live().Rotate(places);
        PositionItems();
    }

    public bool SetFront(int index)
    {
        if (index < 0 || index >= Live().Count)
            return false;
        Live().Rotate(index - _items.FrontIndex);
        PositionItems();
        return true;
    }

    public bool UseFront()
    {
        if (IsOpening || FrontContainer is not { } container)
            return false;

        return container.Kind switch
        {
            CardContainerKind.Box => container.SetLidOpen(!container.IsLidOpen),
            CardContainerKind.Pack => EmptyIntoHand(container),
            _ => OpenInHand(container)
        };
    }

    public bool PutOneDown()
    {
        if (IsOpening || FrontContainer is not { IsLidOpen: true, Storage: { Count: > 0 } storage }
                      || _anchor is not Camera3D eye)
            return false;

        var from = eye.GlobalPosition;
        var hit = GetWorld3D().DirectSpaceState.IntersectRay(new PhysicsRayQueryParameters3D
        {
            From = from,
            To = from - eye.GlobalBasis.Z * PutOneDownReach,
            CollisionMask = WorldLayer,
            Exclude = CameraRay.OwnBodies(eye),
        });
        if (hit.Count == 0 || hit["normal"].AsVector3().Y < LevelEnough)
            return false;

        // Turned to the player's heading, so it lies square to the view.
        var heading = new Basis(Vector3.Up, eye.GlobalRotation.Y);
        return storage.TakeOnto(new Transform3D(heading, hit["position"].AsVector3() + Vector3.Up * PutDownLift));
    }

    public bool DiscardFront()
    {
        if (IsOpening || Front is not { } front)
            return false;

        Let(front);
        front.GetParent()?.RemoveChild(front);
        front.QueueFree();
        PositionItems();
        return true;
    }

    public void PositionItems()
    {
        _laidForDrop = false;
        var items = Live().Items;
        for (var i = 0; i < items.Count; i++)
            items[i].Transform = IsExclusive(items[i])
                ? new Transform3D(Basis.Identity, BoxHoldOffset)
                : PoseOf(i, items.Count);
    }

    public Transform3D PoseOf(int index, int count)
    {
        var pose = new CardFan(FanCenter, FanRadius, FanSpreadDegrees, FanFillCount, FanDepthStep).Pose(index, count);
        if (index != _items.FrontIndex)
            return pose;

        // The front item stands out along its own lean (the fan's radius through it) and a little nearer the camera,
        // so it shows over its neighbours.
        var lean = (pose.Basis * CardFan.FacingCamera.Inverse()) * Vector3.Up;
        return new Transform3D(pose.Basis, pose.Origin + lean * FrontRaise + Vector3.Back * FanDepthStep);
    }

    // ASSUMPTION: a drop lays out and lets go of the cards only; booster packs held beside them stay in the hand
    // (Risk Low). The hand-items spec keeps "a card starts aiming a drop, as today", and today's drop held cards only.
    public void PositionCardsForDrop()
    {
        if (_anchor == null)
            return;

        _laidForDrop = true;
        var eye = _anchor.GlobalTransform;
        var forward = -eye.Basis.Z;
        var yaw = Mathf.Atan2(forward.X, forward.Z);
        var faceYaw = Basis.Identity.Rotated(Vector3.Up, yaw + Mathf.DegToRad(180));
        var ahead = forward * HoldDistance;

        var cards = Cards;
        for (var i = 0; i < cards.Count; i++)
        {
            var thickness = cards[i].GetNode("Designer").Get("Thickness").AsSingle();
            cards[i].GlobalTransform = new Transform3D(faceYaw, eye.Origin + ahead + Vector3.Up * (i * thickness));
        }
    }

    // The booster box leaves the hand, used up, and its booster packs take its place, held as picked-up ones are.
    private bool EmptyIntoHand(CardContainer pack)
    {
        var boosters = pack.EmptyIntoHand();
        Live();
        foreach (var booster in boosters)
            TryTake(booster);

        return boosters.Count > 0;
    }

    // A booster's cards all go into the hand, so it opens only while they fit once the booster has left it. Opening
    // marks the booster opened, so the hand lets go of it, and the hand stays busy through _opening.
    private bool OpenInHand(CardContainer booster)
    {
        if (!FrontBoosterFits || !booster.OpenInHand(this))
            return false;

        _opening = booster;
        Live();
        PositionItems();
        return true;
    }

    private void OnWheel(MouseActionArgs args, int step)
    {
        if (args.Pressed)
            Rotate(step);
    }

    // Takes a body out of the order; the caller decides where it goes.
    private void Let(Node3D body)
    {
        _items.Remove(body);
        if (body is not CardContainer)
            _collision.Remove(body);
    }

    private void RestoreCollision(Node3D body)
    {
        if (body is CardContainer container)
        {
            var (layer, mask) = _collision.TryGetValue(body, out var saved) ? saved : (1u, 1u);
            _collision.Remove(body);
            container.CollisionLayer = layer;
            container.CollisionMask = mask;
        }
        else if (body is RigidBody3D card)
        {
            if (card.GetNodeOrNull<CollisionShape3D>("CardCollision") is { } shape)
                shape.Disabled = false;
            card.CollisionLayer = CardCollisionLayer;
        }
    }

    private void EnablePhysicsDeferred(RigidBody3D card)
    {
        if (!GodotObject.IsInstanceValid(card))
            return;
        card.Freeze = false;
        RestoreCollision(card);
    }

    private Node? SpawnerNode()
    {
        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return null;
        }

        return _services.Get<ICardSpawner>().GetNode();
    }

    // The booster opening in the hand, or null once its cards are in the hand or it was freed.
    private CardContainer? OpeningInHand()
    {
        if (_opening != null && (!GodotObject.IsInstanceValid(_opening) || !_opening.IsOpeningInHand))
            _opening = null;

        return _opening;
    }

    // What the hand still holds. A body that was freed, opened (a booster opening, an emptied booster box) or taken
    // off the hand anchor without the hand letting go (a load clears the scene, a slot locks it) leaves the order
    // here, through HandItems so the front stays right, and the ones left close up.
    private HandItems<Node3D> Live()
    {
        var gone = _items.Items.Where(body => !StillHeld(body)).ToList();
        if (gone.Count == 0)
            return _items;

        foreach (var body in gone)
        {
            _items.Remove(body);
            _collision.Remove(body);
        }

        if (_laidForDrop)
            PositionCardsForDrop();
        else
            PositionItems();
        return _items;
    }

    private bool StillHeld(Node3D body) =>
        GodotObject.IsInstanceValid(body) && body.GetParent() == _anchor
                                          && body is not CardContainer { IsOpened: true };

    private static bool IsExclusive(Node3D body) =>
        body is CardContainer { Kind: CardContainerKind.Box or CardContainerKind.Pack };
}
