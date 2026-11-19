using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     A box, pack or booster. A box or pack has a lid (<see cref="SetLidOpen" />) that the player opens in the hand
///     to take its items out one at a time onto a shelf (<see cref="TakeOneInto" />); a held booster opens straight
///     into the hand of cards (<see cref="OpenInHand" />). Opening it whole plays out over
///     a second or two (<see cref="ContainerOpening" />): the container falls to the surface below it, its lid, flaps
///     or tear strip open, and its packs, boosters or cards (signatures from <see cref="CardPackGenerator" />) rise
///     out one by one to spots in a column above it that takes no more floor than the container, so opening never
///     throws items through walls or into each other. They hang there while the empty wrapper shrinks away, and drop
///     once it is freed.
/// </summary>
public partial class CardContainer : RigidBody3D, IInteractable, IDependent
{
    // Half the gap between the two columns a box's packs rise to: a pack is 0.38 m wide.
    private const float BoxColumnOffset = 0.2f;

    // Seconds a box's flaps or a pack's lid take to swing open or shut in the hand.
    private const float LidSeconds = 0.4f;

    /// <summary>
    ///     Raised when the player interacts with a container that can be opened or carried. The holder that
    ///     listens for it decides whether the container is picked up; interacting never opens it directly.
    /// </summary>
    [Signal]
    public delegate void PickUpRequestedEventHandler(CardContainer container);

    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    private IContainerRun? _opening;
    private IServiceResolver? _services;
    private IRandom? _random;
    private List<OpeningPart>? _lidParts;

    // How far the lid is open, from 0 (shut) to 1 (open); it moves toward the target IsLidOpen sets.
    private float _lidProgress;

    /// <summary>What this container is. Decides what opening it produces.</summary>
    [Export]
    public CardContainerKind Kind { get; set; } = CardContainerKind.Booster;

    /// <summary>Scene of the next container down. Required for a box (packs) and a pack (boosters).</summary>
    [Export]
    public PackedScene? ChildScene { get; set; }

    [Export]
    public Node3D? HighlightMesh { get; set; }

    [Export]
    public float InteractionRange { get; set; } = DefaultInteractionRange;

    /// <summary>
    ///     Decides the cards in a booster. Only set when a test assigns it; otherwise a booster rolls its cards
    ///     from <see cref="ContentSeed" /> when it has one, else from the shared random source.
    /// </summary>
    internal CardPackGenerator? Generator { get; set; }

    /// <summary>
    ///     Seed of everything this container opens into, or null while it has none (it then opens from the shared
    ///     random source). A parent or a saved slot sets it; <see cref="SeedForSave" /> draws one on demand.
    /// </summary>
    public ulong? ContentSeed { get; internal set; }

    /// <summary>
    ///     The price the player set for this container in the shop, or null while it asks its market price. It travels
    ///     with the container from slot to slot and is saved with it, as a card's price is.
    /// </summary>
    public int? ShopPrice { get; set; }

    /// <summary>Spawns the cards. Resolved at startup; assignable directly in tests.</summary>
    public ICardSpawningService? Spawning { get; set; }

    public bool IsOpened { get; private set; }

    /// <summary>Items that have left this container while it opens so far.</summary>
    public int SpawnedCount => _opening?.Released ?? 0;

    /// <summary>Items that <see cref="OpenOnto" /> locked into a shelf slot.</summary>
    public int PlacedCount { get; private set; }

    /// <summary>Items still inside this container while it opens.</summary>
    public int PendingCount => _opening?.Pending ?? 0;

    /// <summary>True while a holder carries this container (its parent is the holder's camera).</summary>
    public bool IsHeld => GetParent() is Camera3D;

    /// <summary>True while this booster opens in the player's hand (<see cref="OpenInHand" />).</summary>
    public bool IsOpeningInHand => _opening is BoosterReveal { IsOver: false };

    /// <summary>True for a box or pack, whose flaps or lid open in the hand; a booster has none.</summary>
    public bool HasLid => Kind != CardContainerKind.Booster;

    /// <summary>True while the lid or flaps are open (<see cref="SetLidOpen" />).</summary>
    public bool IsLidOpen { get; private set; }

    /// <summary>
    ///     Items taken out one at a time (<see cref="TakeOneInto" />). A save stores it, so a reload keeps them out.
    /// </summary>
    public int TakenCount { get; internal set; }

    /// <summary>Items still inside: the 8 it was packed with, less <see cref="TakenCount" />.</summary>
    public int Remaining => CardContainerLayout.ItemsPerContainer - TakenCount;

    public bool CanInteract => !IsOpened && !IsHeld && HasContents;

    public Node3D InteractionBody => this;

    public string Prompt => $"Pick up {CardContainerKindText.NameOf(Kind)}";

    // A booster with no test Generator, no seed and no injected IRandom stays closed, as it did before seeds existed.
    private bool HasContents =>
        Kind == CardContainerKind.Booster
            ? Spawning != null && (Generator != null || ContentSeed != null || _random != null)
            : ChildScene != null;

    public void Inject(IServiceResolver services) => _services = services;

    // Sealed containers in this group are saved by GameSaveService, so a slot restores what was left on the floor.
    // Every reparent (a pick-up, a drop, a slot locking it) leaves the tree, which stops processing, so a lid still
    // swinging picks its motion up again here.
    public override void _EnterTree()
    {
        AddToGroup("card_containers");
        if (IsNodeReady())
            SetProcess(LidMoving);
    }

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        SetProcess(false);
        PoseLid();
        // The card highlighter finds every container through this group, including ones Open() spawns later.
        AddToGroup("Containers");

        if (_services == null)
        {
            ILog.Error($"{Name} was not injected");
            return;
        }

        _services.Get<IRandom>(rng => _random = rng);
        _services.Get<ICardSpawningService>(service => Spawning ??= service);
    }

    /// <summary>
    ///     The seed to write into a save: the one this container has, else one drawn now from the shared random
    ///     source (the only time a container draws a seed, so opening unseeded boxes keeps its draw order). Null when
    ///     no random source was injected.
    /// </summary>
    internal ulong? SeedForSave()
    {
        if (ContentSeed == null && _random != null)
            ContentSeed = ((ulong)_random.Randi() << 32) | _random.Randi();

        return ContentSeed;
    }

    public override void _Process(double delta)
    {
        if (_opening != null)
        {
            if (_opening.Advance((float)delta))
                QueueFree();
            return;
        }

        _lidProgress = Mathf.MoveToward(_lidProgress, LidTarget, (float)delta / LidSeconds);
        PoseLid();
        if (!LidMoving)
            SetProcess(false);
    }

    /// <summary>
    ///     Opens or closes the flaps of a box or the lid of a pack. They swing over a fraction of a second, or move at
    ///     once when <paramref name="instantly" /> (a load). Nothing comes out: items leave an open container one at a
    ///     time through <see cref="TakeOneInto" />. False for a booster or a container already opened.
    /// </summary>
    public bool SetLidOpen(bool open, bool instantly = false)
    {
        if (!HasLid || IsOpened)
            return false;

        IsLidOpen = open;
        if (instantly)
        {
            _lidProgress = LidTarget;
            PoseLid();
        }

        SetProcess(LidMoving);
        return true;
    }

    private float LidTarget => IsLidOpen ? 1f : 0f;

    private bool LidMoving => _opening == null && !Mathf.IsEqualApprox(_lidProgress, LidTarget);

    // The parts keep their rest pose from their own _Ready, so they are posed only once this container is ready. A
    // booster's tear strip is left to its opening.
    private void PoseLid()
    {
        if (!HasLid || !IsNodeReady())
            return;

        _lidParts ??= GetNodeOrNull<Node3D>("Visual") is { } visual
            ? OpeningPart.Under(visual)
            : new List<OpeningPart>();
        foreach (var part in _lidParts)
            part.Pose(_lidProgress);
    }

    /// <summary>
    ///     Takes the next item out of this open box or pack and locks it into <paramref name="slot" />. The item gets
    ///     the seed <see cref="Open" /> would have given it, so taking a box apart by hand gives the same stock. When
    ///     the last item leaves, the container is used up: it counts as opened and is freed. False, with nothing
    ///     taken, when the lid is shut, nothing is left or the slot refuses the item.
    /// </summary>
    public bool TakeOneInto(DeckSlot slot)
    {
        if (!IsLidOpen || IsOpened || Remaining <= 0)
            return false;

        if (SpawnContainer(slot, slot.SeatPosition(slot.Cards.Count), TakenCount) is not CardContainer item)
            return false;

        if (!slot.TryLock(item))
        {
            // Freed at once, before any physics step can report it entering the slot's trigger.
            slot.RemoveChild(item);
            item.Free();
            return false;
        }

        TakenCount++;
        if (Remaining == 0)
        {
            IsOpened = true;
            ClearHighlight();
            QueueFree();
        }

        return true;
    }

    // A wrapper taken out of the tree mid-opening (a load clears the scene's containers) lets its rising items fall.
    public override void _ExitTree()
    {
        _opening?.Abandon();
        SetProcess(false);
    }

    /// <summary>
    ///     Ends an opening in progress at once, as if its time had run out: every item is at its spot and the wrapper
    ///     is freed. A save calls it first, so it never has to store a half-opened container. Does nothing when the
    ///     container is not opening.
    /// </summary>
    public void FinishOpening()
    {
        if (_opening == null || _opening.IsOver)
            return;

        _opening.Finish();
        QueueFree();
    }

    public void Interact()
    {
        if (CanInteract)
            EmitSignal(SignalName.PickUpRequested, this);
    }

    public void Highlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = true;
    }

    public void ClearHighlight()
    {
        if (HighlightMesh != null)
            HighlightMesh.Visible = false;
    }

    /// <summary>
    ///     Offset of the n-th item opened out of a container of <paramref name="kind" />: above the container, one
    ///     <see cref="StackStep" /> higher per item. A box's packs fill two columns side by side, so its column stays
    ///     under the shop's 3.5 m ceiling when the box stands on the floor.
    /// </summary>
    public static Vector3 SpawnOffset(int index, CardContainerKind kind)
    {
        if (kind != CardContainerKind.Box)
            return new Vector3(0f, LiftHeight(kind) + index * StackStep(kind), 0f);

        var side = index % 2 == 0 ? -BoxColumnOffset : BoxColumnOffset;
        return new Vector3(side, LiftHeight(kind) + index / 2 * StackStep(kind), 0f);
    }

    /// <summary>
    ///     Height above the container's origin of the first item it opens into: a little more than the container's
    ///     own height (a box is 0.60 m tall, a pack 0.37 m, a booster 0.045 m).
    /// </summary>
    public static float LiftHeight(CardContainerKind kind)
    {
        return kind switch
        {
            CardContainerKind.Box => 0.65f,
            CardContainerKind.Pack => 0.42f,
            CardContainerKind.Booster => 0.1f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind")
        };
    }

    /// <summary>
    ///     Vertical gap between the items opened out of <paramref name="kind" />: a little more than the height of
    ///     one item (a pack is 0.37 m tall, a booster 0.045 m, a card 0.005 m), so none spawns inside the one below.
    /// </summary>
    public static float StackStep(CardContainerKind kind)
    {
        return kind switch
        {
            CardContainerKind.Box => 0.42f,
            CardContainerKind.Pack => 0.1f,
            CardContainerKind.Booster => 0.05f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind")
        };
    }

    /// <summary>
    ///     Opens the container: it falls to the surface below it, opens, and its contents rise out one by one.
    ///     Returns false when it was already opened or lacks what it needs to open.
    /// </summary>
    public bool Open()
    {
        if (!CanInteract || GetParent() is not Node3D parent)
            return false;

        var settled = BeginOpening();
        var spawns = Kind == CardContainerKind.Booster
            ? CardSpawns(parent, settled.Origin)
            : ContainerSpawns(parent, settled.Origin, TakenCount);
        StartOpening(settled, spawns);
        return true;
    }

    /// <summary>
    ///     Opens this held booster in front of the player (<see cref="BoosterReveal" />): its strip tears off, its
    ///     cards spread into a fan, and the fan slides into <paramref name="hand" />. The cards are the ones
    ///     <see cref="Open" /> would let out, from the same seed. The booster stays under the hand anchor the whole
    ///     time and is freed when the cards are in the hand. False unless this is a held, unopened booster that has
    ///     its contents.
    /// </summary>
    public bool OpenInHand(ICardHolder hand)
    {
        if (Kind != CardContainerKind.Booster || IsOpened || !IsHeld || !HasContents
            || GetParent() is not Node3D anchor)
            return false;

        IsOpened = true;
        ClearHighlight();
        _opening = new BoosterReveal(this, anchor, hand, CardGenerator().OpenBooster(), Spawning!);
        SetProcess(true);
        return true;
    }

    /// <summary>
    ///     Unpacks a box or pack onto a shelf: the items still inside are spawned and locked into
    ///     <paramref name="freeSlots" /> in order, and the rest spill onto the floor above the container, as
    ///     <see cref="Open" /> does. An item whose slot refuses it stays loose. Returns false when this container
    ///     cannot be unpacked (a booster, or one that is not openable).
    /// </summary>
    public bool OpenOnto(IReadOnlyList<DeckSlot> freeSlots)
    {
        if (Kind == CardContainerKind.Booster || !CanInteract || GetParent() is not Node3D parent)
            return false;

        var settled = BeginOpening();
        var placeable = Math.Min(Remaining, freeSlots.Count);
        for (var i = 0; i < placeable; i++)
        {
            // The same index as Open() uses, so each placed child gets the seed it would have got when opened loose.
            var child = SpawnContainer(parent, settled.Origin + SpawnOffset(i, Kind), TakenCount + i);
            if (child is CardContainer container && freeSlots[i].TryLock(container))
                PlacedCount++;
        }

        StartOpening(settled, ContainerSpawns(parent, settled.Origin, TakenCount + placeable));
        return true;
    }

    // Marks the container opened and stops it colliding, being aimed at and falling on its own. Returns the pose it
    // settles into, worked out before its collision mask is cleared.
    private Transform3D BeginOpening()
    {
        IsOpened = true;
        ClearHighlight();
        var settled = ContainerOpening.SettledPose(this, CollisionMask);
        CollisionLayer = 0;
        CollisionMask = 0;
        Freeze = true;
        return settled;
    }

    private void StartOpening(Transform3D settled, IReadOnlyList<Func<Node3D?>> spawns)
    {
        _opening = new ContainerOpening(this, settled, spawns, _lidProgress);
        SetProcess(true);
    }

    private List<Func<Node3D?>> CardSpawns(Node3D parent, Vector3 origin)
    {
        var signatures = CardGenerator().OpenBooster();
        var spawns = new List<Func<Node3D?>>(signatures.Length);
        for (var i = 0; i < signatures.Length; i++)
        {
            var signature = signatures[i];
            var position = origin + SpawnOffset(i, Kind);
            spawns.Add(() => SpawnCard(parent, signature, position));
        }

        return spawns;
    }

    // A test-set Generator, else one drawing from the saved content seed, else from the shared random source.
    private CardPackGenerator CardGenerator() =>
        Generator ?? new CardPackGenerator(ContentSeed is { } seed ? RandomSources.Create(seed) : _random!);

    // The items from startIndex on, each with its own seed index. They take the column spots from the bottom up, so
    // the ones left over after an unpack start at the lowest spot.
    private List<Func<Node3D?>> ContainerSpawns(Node3D parent, Vector3 origin, int startIndex)
    {
        var spawns = new List<Func<Node3D?>>();
        for (var i = startIndex; i < CardContainerLayout.ItemsPerContainer; i++)
        {
            var index = i;
            var position = origin + SpawnOffset(index - startIndex, Kind);
            spawns.Add(() => SpawnContainer(parent, position, index));
        }

        return spawns;
    }

    private Node3D? SpawnCard(Node3D parent, CardSignature signature, Vector3 position)
    {
        if (!GodotObject.IsInstanceValid(parent))
            return null;

        var transform = new Transform3D(Basis.Identity, position);
        return Spawning?.SpawnCard(signature, transform, parent);
    }

    // Returns the spawned child, or null when nothing was spawned.
    private Node3D? SpawnContainer(Node3D parent, Vector3 position, int index)
    {
        if (!GodotObject.IsInstanceValid(parent) || ChildScene?.Instantiate() is not Node3D child)
            return null;

        if (child is CardContainer container)
        {
            // Children share this container's card spawner. A seeded parent seeds each child before it enters the
            // tree; a test-set Generator still passes down unchanged.
            container.Generator = Generator;
            container.Spawning = Spawning;
            if (ContentSeed is { } seed)
                container.ContentSeed = ContainerSeeds.ChildSeed(seed, index);
        }

        parent.AddChild(child);
        child.GlobalPosition = position;
        return child;
    }
}
