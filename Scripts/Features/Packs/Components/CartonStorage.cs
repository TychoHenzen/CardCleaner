using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Components;
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
///     player throws it away. The box shows what it holds once its flaps open (<see cref="CartonView" />).
/// </summary>
public partial class CartonStorage : Node3D, IDependent
{
    // The space over the box's top that catches what lands there: the box's footprint, from just below its top.
    private static readonly Vector3 CatchSize = new(0.8f, 0.3f, 0.86f);
    private static readonly Transform3D CatchOffset = new(Basis.Identity, new Vector3(0f, 0.75f, 0f));

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

    /// <summary>How many items the box holds that <paramref name="accepts" /> takes.</summary>
    public int CountOf(System.Func<StoredItem, bool> accepts) => Items.Count(accepts);

    /// <summary>
    ///     The stock of the last item <paramref name="slot" /> would take now, or null when it takes none.
    /// </summary>
    public StockKind? NextFor(DeckSlot slot)
    {
        var index = Shown.LastIndexOf(item => slot.CanTake(ShelfPlacementRule.From(item)));
        return index < 0 ? null : ShelfPlacementRule.From(Items[index]);
    }

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
    ///     Takes out the last item <paramref name="slot" /> takes and locks it there. False, with nothing taken, when
    ///     the slot takes none of the items or refuses the one built.
    /// </summary>
    public bool TakeInto(DeckSlot slot)
    {
        // The slot is asked about what the box shows first, so a slot that takes none of it draws no seed.
        if (Box is not { } box || Shown.LastIndexOf(item => slot.CanTake(ShelfPlacementRule.From(item))) < 0)
            return false;

        var contents = Contents();
        var index = contents.LastIndexOf(item => slot.CanTake(ShelfPlacementRule.From(item)));
        if (index < 0)
            return false;

        var seat = new Transform3D(slot.GlobalBasis, slot.SeatPosition(slot.Cards.Count));
        if (StoredItemBodies.Build(contents.Items[index], slot, seat, box) is not { } body)
            return false;

        if (!slot.TryLock(body))
        {
            // Out of the tree at once, before any physics step can report it entering the slot's trigger.
            slot.RemoveChild(body);
            body.QueueFree();
            return false;
        }

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
    ///     Moves items into <paramref name="slots" /> while any slot takes one, the last item in first, and returns how
    ///     many moved. A slot that stacks boosters is filled before the next one is started.
    /// </summary>
    public int MoveInto(IReadOnlyList<DeckSlot> slots)
    {
        var moved = 0;
        while (NextMove(slots) is { } slot && TakeInto(slot))
            moved++;

        return moved;
    }

    private DeckSlot? NextMove(IReadOnlyList<DeckSlot> slots)
    {
        var items = Items;
        for (var index = items.Count - 1; index >= 0; index--)
        {
            var stock = ShelfPlacementRule.From(items[index]);
            if (slots.FirstOrDefault(slot => slot.CanTake(stock)) is { } slot)
                return slot;
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
