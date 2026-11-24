using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Hand.Components;

/// <summary>
///     The player's one hand (<see cref="IHand" />): the live bodies of the held cards and sealed containers, kept in
///     order by <see cref="HandItems{T}" />. The wheel moves the front and Q sets the front item down in front of the
///     player. <see cref="HeldBodies" /> moves the bodies in and out, <see cref="HandLayout" /> poses them (its
///     constants say what each layout export does), and <see cref="HandContainerUse" /> opens the front container.
/// </summary>
public partial class Hand : Node3D, IHand, IDependent
{
    [Export] public uint CardCollisionLayerExport = HandLayout.CardCollisionLayer;
    [Export] public float HoldDistance = HandLayout.HoldDistance;
    [Export] public Vector3 FanCenter = HandLayout.FanCenter;
    [Export] public float FanRadius = HandLayout.FanRadius;
    [Export] public float FanSpreadDegrees = HandLayout.FanSpreadDegrees;
    [Export] public int FanFillCount = HandLayout.FanFillCount;
    [Export] public float FanDepthStep = HandLayout.FanDepthStep;
    [Export] public float FrontRaise = HandLayout.FrontRaise;
    [Export] public float PutDownDistance = HandLayout.PutDownDistance;
    [Export] public Vector3 BoxHoldOffset = HandLayout.BoxHoldOffset;

    private readonly HandItems<Node3D> _items = new(IsExclusive);
    private readonly HeldBodies _bodies;
    private readonly HandContainerUse _containers;

    private IServiceResolver? _services;
    private IInputService? _input;
    private Node3D? _anchor;

    // True while the cards lie in the drop layout (PositionCardsForDrop) rather than the fan.
    private bool _laidForDrop;

    public Hand()
    {
        _bodies = new HeldBodies(() => CardCollisionLayerExport);
        _containers = new HandContainerUse(this);
    }

    public override bool _PropertyCanRevert(StringName property) =>
        HandLayout.TryDefault(property, out _) || base._PropertyCanRevert(property);

    public override Variant _PropertyGetRevert(StringName property) =>
        HandLayout.TryDefault(property, out var value) ? value : base._PropertyGetRevert(property);

    public IReadOnlyList<Node3D> Items => Live().Items;
    public int Count => Live().Count;
    public int FrontIndex => Live().FrontIndex;
    public Node3D? Front => Live().Front;
    public int Room => IsOpening ? 0 : Live().Room;
    public IReadOnlyList<RigidBody3D> Cards => Live().Items.OfType<CardController>().Cast<RigidBody3D>().ToList();
    public bool IsOpening => _containers.Opening != null;
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
            _input.RegisterAction(this, "hand_next", MouseButton.WheelUp, args => { if (args.Pressed) Rotate(1); });
            _input.RegisterAction(this, "hand_previous", MouseButton.WheelDown,
                args => { if (args.Pressed) Rotate(-1); });
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
        if (!CanTake(body) || body is not RigidBody3D rigid || _anchor == null)
            return false;

        _bodies.Hold(rigid, _anchor);
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

        _items.Remove(body);
        _bodies.PlaceIn(body, world);
        PositionItems();
        return true;
    }

    // A look at the front item must not draw a container's seed, so Peek reads the record as it stands.
    public StoredItem? Peek() =>
        !IsOpening && Front is RigidBody3D front && StoredItemBodies.IsRecorded(front)
            ? StoredItemBodies.CurrentRecordOf(front)
            : null;

    public bool CanPut(StoredItem item) => RoomFor(item) > 0;

    public int RoomFor(StoredItem item) => IsOpening || _anchor == null ? 0 : HandRoom.For(item, Count, Room);

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

        _bodies.Free(body);
        return false;
    }

    // The front body leaves as its record (a container without a seed draws one now) and is freed.
    public StoredItem? TakeLast()
    {
        if (IsOpening || Front is not RigidBody3D front || !StoredItemBodies.IsRecorded(front))
            return null;

        var record = StoredItemBodies.RecordOf(front);
        _items.Remove(front);
        _bodies.Free(front);
        PositionItems();
        return record;
    }

    public bool PutFrontDown()
    {
        if (IsOpening || Front is not RigidBody3D front || _anchor == null || SpawnerNode() is not { } world)
            return false;

        _items.Remove(front);
        _bodies.SetDown(front, world, _anchor.GlobalPosition - _anchor.GlobalBasis.Z * PutDownDistance);
        PositionItems();
        return true;
    }

    public bool ReleaseCard(RigidBody3D card)
    {
        if (IsOpening || card is not CardController || !Live().Items.Contains(card)
            || SpawnerNode() is not { } world)
            return false;

        _items.Remove(card);
        _bodies.Drop(card, world);
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

    public bool UseFront() => !IsOpening && FrontContainer is { } container && _containers.Use(container);

    public bool PutOneDown() =>
        !IsOpening && FrontContainer is { IsLidOpen: true, Storage: { Count: > 0 } storage }
                   && _anchor is Camera3D eye && HeldBodies.PutDownSpot(eye) is { } spot
                   && storage.TakeOnto(spot);

    public bool DiscardFront()
    {
        if (IsOpening || Front is not { } front)
            return false;

        _items.Remove(front);
        _bodies.Free(front);
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

    public Transform3D PoseOf(int index, int count) =>
        HandLayout.FanPose(new CardFan(FanCenter, FanRadius, FanSpreadDegrees, FanFillCount, FanDepthStep),
            index, count, index == _items.FrontIndex, FrontRaise, FanDepthStep);

    // ASSUMPTION: a drop lays out and lets go of the cards only; booster packs held beside them stay in the hand
    // (Risk Low). The hand-items spec keeps "a card starts aiming a drop, as today", and today's drop held cards only.
    public void PositionCardsForDrop()
    {
        if (_anchor == null)
            return;

        _laidForDrop = true;
        var cards = Cards;
        for (var i = 0; i < cards.Count; i++)
        {
            var thickness = cards[i].GetNode("Designer").Get("Thickness").AsSingle();
            cards[i].GlobalTransform = HandLayout.DropPose(_anchor.GlobalTransform, HoldDistance, i, thickness);
        }
    }

    private Node? SpawnerNode()
    {
        if (_services != null)
            return _services.Get<ICardSpawner>().GetNode();

        ILog.Error($"{Name} was not injected");
        return null;
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
            _bodies.Forget(body);
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
