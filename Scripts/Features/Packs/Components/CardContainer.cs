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
///     A box, pack or booster the player opens by interacting. Opening plays out over a second or two
///     (<see cref="ContainerOpening" />): the container falls to the surface below it, its lid, flaps or tear strip
///     open, and its 8 packs, boosters or cards (signatures from <see cref="CardPackGenerator" />) rise out one by one
///     to spots in a column above it that takes no more floor than the container, so opening never throws items
///     through walls or into each other. They hang there while the empty wrapper shrinks away, and drop once it is
///     freed.
/// </summary>
public partial class CardContainer : RigidBody3D, IInteractable, IDependent
{
    // Half the gap between the two columns a box's packs rise to: a pack is 0.38 m wide.
    private const float BoxColumnOffset = 0.2f;

    /// <summary>
    ///     Raised when the player interacts with a container that can be opened or carried. The holder that
    ///     listens for it decides whether the container is picked up; interacting never opens it directly.
    /// </summary>
    [Signal]
    public delegate void PickUpRequestedEventHandler(CardContainer container);

    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    private ContainerOpening? _opening;
    private IServiceResolver? _services;
    private IRandom? _random;

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
    public override void _EnterTree() => AddToGroup("card_containers");

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        SetProcess(false);
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
        if (_opening?.Advance((float)delta) == true)
            QueueFree();
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
            : ContainerSpawns(parent, settled.Origin, 0);
        StartOpening(settled, spawns);
        return true;
    }

    /// <summary>
    ///     Unpacks a box or pack onto a shelf: the first items it holds are spawned and locked into
    ///     <paramref name="freeSlots" /> in order, and the rest spill onto the floor above the container, as
    ///     <see cref="Open" /> does. An item whose slot refuses it stays loose. Returns false when this container
    ///     cannot be unpacked (a booster, or one that is not openable).
    /// </summary>
    public bool OpenOnto(IReadOnlyList<DeckSlot> freeSlots)
    {
        if (Kind == CardContainerKind.Booster || !CanInteract || GetParent() is not Node3D parent)
            return false;

        var settled = BeginOpening();
        var placeable = Math.Min(CardContainerLayout.ItemsPerContainer, freeSlots.Count);
        for (var i = 0; i < placeable; i++)
        {
            // The same index as Open() uses, so each placed child gets the seed it would have got when opened loose.
            var child = SpawnContainer(parent, settled.Origin + SpawnOffset(i, Kind), i);
            if (child is CardContainer container && freeSlots[i].TryLock(container))
                PlacedCount++;
        }

        StartOpening(settled, ContainerSpawns(parent, settled.Origin, placeable));
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
        _opening = new ContainerOpening(this, settled, spawns);
        SetProcess(true);
    }

    private List<Func<Node3D?>> CardSpawns(Node3D parent, Vector3 origin)
    {
        var generator = Generator ?? new CardPackGenerator(ContentSeed is { } seed
            ? RandomSources.Create(seed)
            : _random!);
        var signatures = generator.OpenBooster();
        var spawns = new List<Func<Node3D?>>(signatures.Length);
        for (var i = 0; i < signatures.Length; i++)
        {
            var signature = signatures[i];
            var position = origin + SpawnOffset(i, Kind);
            spawns.Add(() => SpawnCard(parent, signature, position));
        }

        return spawns;
    }

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
