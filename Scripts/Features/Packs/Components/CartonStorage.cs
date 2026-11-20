using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Hand.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Shop.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     What a cardboard box holds, and how items get in and out of it. A box comes packed with eight booster boxes.
///     While it stands free with its flaps open, a card, booster pack or booster box that lands on top of it goes in
///     where the packer finds room (<see cref="BoxContents" />); one that does not fit stays where it lies. Items come
///     out last in, first out: into a shelf slot (<see cref="TakeInto" />), onto the floor (<see cref="TakeOnto" />),
///     or into every free slot of a shelf at once (<see cref="MoveInto" />). An emptied box stays a box until the
///     player throws it away. The box shows what it holds once its flaps open (<see cref="CartonView" />). As an item
///     store (<see cref="IPickingStore" />) an open box takes records in and gives them out last in, first out, or the
///     last one a target accepts when it is held.
/// </summary>
public partial class CartonStorage : Node3D, IDependent, IPickingStore
{
    // The space over the box's top that catches what lands there: the box's footprint, from its top up.
    private static readonly Vector3 CatchSize = new(0.8f, 0.3f, 0.86f);

    private static readonly Transform3D CatchOffset =
        new(Basis.Identity, new Vector3(0f, CartonSpace.OutsideHeight + CatchSize.Y / 2f, 0f));

    // How far below the rim an aim still counts as aimed at the open top.
    private const float OpenTopMargin = 0.05f;

    // Containers lie on layer 1 and cards on layer 2.
    private const uint CatchMask = 0b11;
    private const int MostCaughtPerFrame = 16;

    // A spilled item is set down beside the box, one a little higher than the last.
    private const float SpillSide = 0.65f;
    private const float SpillStep = 0.08f;

    private BoxContents? _contents;
    private BoxContents? _preview;
    private CartonView? _view;
    private IServiceResolver? _services;
    private PhysicsShapeQueryParameters3D? _catchQuery;

    // Bodies that found no room, so a body resting on a full box is not packed again every frame. Any change to what
    // the box holds clears it, since room may have opened up.
    private readonly HashSet<ulong> _noRoom = new();

    /// <summary>True until anything goes in or out: the box still holds the eight booster boxes it came with.</summary>
    public bool IsFresh => _contents == null;

    /// <summary>How many items the box holds.</summary>
    public int Count => Shown.Count;

    /// <summary>
    ///     What the box holds, in the order the items went in. A fresh box lists its booster boxes unseeded.
    /// </summary>
    public IReadOnlyList<StoredItem> Items => Shown.Items;

    /// <summary>What a save stores: the items once any went in or out, or null for a fresh box.</summary>
    public IReadOnlyList<StoredItem>? SavedItems => _contents?.Items;

    private CardContainer? Box => GetParentOrNull<CardContainer>();

    // A fresh box is shown and counted from an unseeded copy of what it came with, so looking draws no seed.
    private BoxContents Shown => _contents ?? (_preview ??= Packed(FreshItems(null)));

    public void Inject(IServiceResolver services) => _services = services;

    /// <summary>
    ///     Sets what the box holds, as a save stored it: <paramref name="items" /> in order, or a fresh box when null.
    ///     Called before the box enters the tree.
    /// </summary>
    public void Restore(IReadOnlyList<StoredItem>? items)
    {
        _contents = null;
        _noRoom.Clear();
        if (items != null)
        {
            var (contents, unfit) = BoxContents.Packed(CartonSpace.Packer(), items);
            _contents = contents;
            if (unfit.Count > 0)
                ILog.Error($"{unfit.Count} saved items no longer fit their cardboard box and were left out");
        }

        _view?.Show(Shown);
    }

    /// <summary>Shows the box's contents; the box calls it when its flaps open.</summary>
    public void ShowContents()
    {
        if (_view == null)
        {
            _view = new CartonView { Name = "Contents" };
            AddChild(_view);
        }

        _view.Show(Shown);
    }

    /// <summary>The item put in last, read from what the box shows, so a fresh box draws no seed.</summary>
    public StoredItem? Peek() => PeekLast(static _ => true);

    /// <summary>The last item <paramref name="accepts" /> allows; looking draws no seed.</summary>
    public StoredItem? PeekLast(System.Func<StoredItem, bool> accepts)
    {
        var index = Shown.LastIndexOf(accepts);
        return index < 0 ? null : Shown.Items[index];
    }

    /// <summary>
    ///     True when the box's flaps are open and the packer finds room for <paramref name="item" />: a card, booster
    ///     pack or booster box, never another cardboard box.
    /// </summary>
    // ASSUMPTION: a shut box takes nothing through a click either, as it catches nothing that lands on it (Risk Low).
    public bool CanPut(StoredItem item) =>
        Box is { IsLidOpen: true }
        && item is StoredCard or StoredContainer { Kind: CardContainerKind.Booster or CardContainerKind.Pack }
        && Shown.HasRoomFor(item.Shape);

    /// <summary>
    ///     Puts <paramref name="item" /> in where the packer finds room; false, with nothing changed, otherwise.
    /// </summary>
    public bool Put(StoredItem item)
    {
        if (!CanPut(item) || Contents().With(item) is not { } contents)
            return false;

        Change(contents);
        return true;
    }

    /// <summary>Takes out the item put in last.</summary>
    public StoredItem? TakeLast() => TakeLast(static _ => true);

    /// <summary>
    ///     Takes out the last item <paramref name="accepts" /> allows and returns its record; the items after it are
    ///     packed again, and one that no longer fits is set down beside the box. Null, with no seed drawn, when there
    ///     is none.
    /// </summary>
    public StoredItem? TakeLast(System.Func<StoredItem, bool> accepts)
    {
        if (Box is not { } box || Shown.LastIndexOf(accepts) < 0)
            return null;

        var contents = Contents();
        var index = contents.LastIndexOf(accepts);
        if (index < 0)
            return null;

        var item = contents.Items[index];
        TakeAt(index, box);
        return item;
    }

    /// <summary>How many items the box holds that <paramref name="accepts" /> takes.</summary>
    public int CountOf(System.Func<StoredItem, bool> accepts) => Items.Count(accepts);

    /// <summary>
    ///     The part of the box a point on it (world space) lies on: its open top, where a click puts the hand's item,
    ///     while the box is open, else its stock.
    /// </summary>
    public AimPart PartAt(Vector3 point) =>
        Box is { IsLidOpen: true } box && box.ToLocal(point).Y >= CartonSpace.OutsideHeight - OpenTopMargin
            ? AimPart.Space
            : AimPart.Stock;

    public override void _PhysicsProcess(double delta)
    {
        if (Box is not { } box || !IsCatching(box))
            return;

        // One query, built once: a body keeps its RID when it moves, so only the transform changes each frame.
        _catchQuery ??= new PhysicsShapeQueryParameters3D
        {
            Shape = new BoxShape3D { Size = CatchSize },
            CollisionMask = CatchMask,
            CollideWithAreas = false,
            Exclude = [box.GetRid()],
        };
        _catchQuery.Transform = box.GlobalTransform * CatchOffset;
        foreach (var hit in GetWorld3D().DirectSpaceState.IntersectShape(_catchQuery, MostCaughtPerFrame))
        {
            if (hit["collider"].AsGodotObject() is RigidBody3D body)
                TryPut(body);
        }
    }

    /// <summary>
    ///     Puts <paramref name="body" /> in the box when the box keeps it (<see cref="StoredItemBodies.ShapeOf" />), it
    ///     lies free in the world and there is room; the body is freed. False, with the body left alone, otherwise.
    /// </summary>
    public bool TryPut(RigidBody3D body)
    {
        if (body == Box || !DeckSlot.IsFreeStock(body) || body.GetParent() is Camera3D
            || StoredItemBodies.ShapeOf(body) is not { } shape || _noRoom.Contains(body.GetInstanceId()))
            return false;

        // Room is checked on what the box shows, which packs the same shapes in the same places, so an item that does
        // not fit draws no seed: a fresh box stays fresh and the landing container stays unseeded.
        if (!Shown.HasRoomFor(shape))
        {
            _noRoom.Add(body.GetInstanceId());
            return false;
        }

        if (Contents().With(StoredItemBodies.RecordOf(body)) is not { } contents)
            return false;

        Change(contents);
        body.GetParent()?.RemoveChild(body);
        body.QueueFree();
        return true;
    }

    /// <summary>
    ///     Takes out the last item <paramref name="store" /> takes and puts it there. False, with nothing taken, when
    ///     the store takes none of the items or refuses the one taken. Unlike <see
    ///     cref="TakeLast(System.Func{StoredItem, bool})" /> followed by a put, this works on a shut box too: the
    ///     restocker unpacks a box without opening it.
    /// </summary>
    public bool TakeInto(IItemStore store)
    {
        // The store is asked about what the box shows first, so a store that takes none of it draws no seed.
        if (Box is not { } box || Shown.LastIndexOf(store.CanPut) < 0)
            return false;

        var contents = Contents();
        var index = contents.LastIndexOf(store.CanPut);
        if (index < 0 || !store.Put(contents.Items[index]))
            return false;

        TakeAt(index, box);
        return true;
    }

    /// <summary>
    ///     Takes out the last item and sets it down loose at <paramref name="at" />. False when the box is empty.
    /// </summary>
    public bool TakeOnto(Transform3D at)
    {
        if (Box is not { } box || Count == 0)
            return false;

        var contents = Contents();
        if (StoredItemBodies.Build(contents.Items[^1], LooseParent(box), at, box) == null)
            return false;

        TakeAt(contents.Count - 1, box);
        return true;
    }

    /// <summary>
    ///     Moves items into <paramref name="stores" /> while any store takes one, the last item in first, and returns
    ///     how many moved. The first store that takes an item gets it, so one shelf level fills before the next.
    /// </summary>
    public int MoveInto(IReadOnlyList<IItemStore> stores)
    {
        var moved = 0;
        while (NextMove(stores) is { } store && TakeInto(store))
            moved++;

        return moved;
    }

    private IItemStore? NextMove(IReadOnlyList<IItemStore> stores)
    {
        var items = Shown.Items;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            var item = items[index];
            if (stores.FirstOrDefault(store => store.CanPut(item)) is { } store)
                return store;
        }

        return null;
    }

    // Takes the item out of the data, sets down any later item that no longer fits once the rest is packed again, and
    // shows the change.
    private void TakeAt(int index, CardContainer box)
    {
        var (contents, spilled) = Contents().Without(index);
        Change(contents);
        for (var i = 0; i < spilled.Count; i++)
        {
            var beside = box.GlobalTransform.Origin + box.GlobalBasis.X * SpillSide
                         + Vector3.Up * (SpillStep * (i + 1));
            if (StoredItemBodies.Build(spilled[i], LooseParent(box), new Transform3D(Basis.Identity, beside), box)
                == null)
                ILog.Error($"A {spilled[i].Shape} spilled from a cardboard box could not be set down and was lost");
        }
    }

    private void Change(BoxContents contents)
    {
        _contents = contents;
        _noRoom.Clear();
        _view?.Show(contents);
    }

    // A box catches what lands on it only while it stands in the world (not carried, not on a cleared layer) with its
    // flaps open.
    // ASSUMPTION: a shut box catches nothing (Risk Low), so a delivery stacked on a sealed carton stays on top of it.
    private static bool IsCatching(CardContainer box) =>
        box.IsLidOpen && !box.IsOpened && !box.IsHeld && box.CollisionLayer != 0;

    // The first time anything goes in or out, the box's own booster boxes become real items, the n-th seeded with
    // ContainerSeeds.ChildSeed of the box's seed and n. The last one in comes out first, so the first taken gets child
    // seed 0.
    private BoxContents Contents()
    {
        if (_contents != null)
            return _contents;

        _contents = Packed(FreshItems(Box?.SeedForSave()).Reverse());
        _preview = null;
        return _contents;
    }

    private IEnumerable<StoredItem> FreshItems(ulong? seed)
    {
        var scene = Box?.ChildScene?.ResourcePath;
        if (string.IsNullOrEmpty(scene))
            yield break;

        for (var index = 0; index < CardContainerLayout.ItemsPerContainer; index++)
        {
            yield return new StoredContainer(scene, CardContainerKind.Pack,
                seed is { } parent ? ContainerSeeds.ChildSeed(parent, index) : null, null);
        }
    }

    private static BoxContents Packed(IEnumerable<StoredItem> items) =>
        BoxContents.Packed(CartonSpace.Packer(), items).Contents;

    // Where loose items go: the card spawner's node, as for a container set down, else the box's own parent.
    private Node3D LooseParent(CardContainer box)
    {
        if (_services != null && _services.Has<ICardSpawner>())
            return _services.Get<ICardSpawner>().GetNode();

        return box.GetParent() is Node3D parent and not Camera3D ? parent : (Node3D)GetTree().CurrentScene;
    }
}
