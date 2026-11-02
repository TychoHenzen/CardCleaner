using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     A box, pack or booster the player opens by looking at it and interacting. Opening a box spawns
///     8 packs, a pack spawns 8 boosters and a booster spawns 8 cards whose signatures come from
///     <see cref="CardPackGenerator" />. The contents appear one per frame (like the debug card spawner)
///     in a column straight above the container, each one a little higher than the last, and physics
///     lets them settle. A column takes no more floor than the container itself, so opening never throws
///     items through the walls of the room the container stands in, and no item spawns inside another.
/// </summary>
public partial class CardContainer : RigidBody3D, IInteractable, IDependent
{
    /// <summary>Height above the container's origin of the first item it opens into.</summary>
    public const float LiftHeight = 0.4f;

    /// <summary>
    ///     Raised when the player interacts with a container that can be opened or carried. The holder that
    ///     listens for it decides whether the container is picked up; interacting never opens it directly.
    /// </summary>
    [Signal]
    public delegate void PickUpRequestedEventHandler(CardContainer container);

    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    private readonly Queue<Action> _pendingSpawns = new();
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

    /// <summary>Items opened out of this container so far.</summary>
    public int SpawnedCount { get; private set; }

    /// <summary>Items still waiting to be spawned over the next frames.</summary>
    public int PendingCount => _pendingSpawns.Count;

    /// <summary>True while a holder carries this container (its parent is the holder's camera).</summary>
    public bool IsHeld => GetParent() is Camera3D;

    public bool CanInteract => !IsOpened && !IsHeld && HasContents;

    public Node3D InteractionBody => this;

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
        if (_pendingSpawns.Count > 0)
        {
            _pendingSpawns.Dequeue().Invoke();
            SpawnedCount++;
        }

        if (_pendingSpawns.Count == 0)
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
    ///     Offset of the n-th item opened out of a container of <paramref name="kind" />: straight above the
    ///     container, one <see cref="StackStep" /> higher per item.
    /// </summary>
    public static Vector3 SpawnOffset(int index, CardContainerKind kind)
    {
        return new Vector3(0f, LiftHeight + index * StackStep(kind), 0f);
    }

    /// <summary>
    ///     Vertical gap between the items opened out of <paramref name="kind" />: a little more than the height of
    ///     one item (a pack is 0.15 m tall, a booster 0.03 m, a card 0.005 m), so none spawns inside the one below.
    /// </summary>
    public static float StackStep(CardContainerKind kind)
    {
        return kind switch
        {
            CardContainerKind.Box => 0.25f,
            CardContainerKind.Pack => 0.1f,
            CardContainerKind.Booster => 0.05f,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown container kind")
        };
    }

    /// <summary>
    ///     Opens the container: it disappears and its contents are queued to appear one per frame.
    ///     Returns false when it was already opened or lacks what it needs to open.
    /// </summary>
    public bool Open()
    {
        if (!CanInteract || GetParent() is not Node3D parent)
            return false;

        IsOpened = true;
        var origin = GlobalPosition;
        ClearHighlight();
        Visible = false;
        CollisionLayer = 0;
        CollisionMask = 0;
        Freeze = true;

        if (Kind == CardContainerKind.Booster)
            QueueCards(parent, origin);
        else
            QueueContainers(parent, origin);

        SetProcess(true);
        return true;
    }

    private void QueueCards(Node3D parent, Vector3 origin)
    {
        var generator = Generator ?? new CardPackGenerator(ContentSeed is { } seed
            ? RandomSources.Create(seed)
            : _random!);
        var signatures = generator.OpenBooster();
        for (var i = 0; i < signatures.Length; i++)
        {
            var signature = signatures[i];
            var position = origin + SpawnOffset(i, Kind);
            _pendingSpawns.Enqueue(() => SpawnCard(parent, signature, position));
        }
    }

    private void QueueContainers(Node3D parent, Vector3 origin)
    {
        for (var i = 0; i < CardContainerLayout.ItemsPerContainer; i++)
        {
            var index = i;
            var position = origin + SpawnOffset(index, Kind);
            _pendingSpawns.Enqueue(() => SpawnContainer(parent, position, index));
        }
    }

    // Each child of a sealed container gets its own seed, derived from this one and the child's index. The parent
    // seed is mixed before the index is added, so neighbouring parent seeds (777, 778) share no child seed.
    private static ulong ChildSeed(ulong parentSeed, int index) =>
        SplitMix64(unchecked(SplitMix64(parentSeed) + (ulong)index));

    // One splitmix64 step: spreads a counter into a well-mixed seed.
    private static ulong SplitMix64(ulong value)
    {
        unchecked
        {
            var z = value + 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
    }

    private void SpawnCard(Node3D parent, CardSignature signature, Vector3 position)
    {
        if (!GodotObject.IsInstanceValid(parent))
            return;

        var transform = new Transform3D(Basis.Identity, position);
        Spawning?.SpawnCard(signature, transform, parent);
    }

    private void SpawnContainer(Node3D parent, Vector3 position, int index)
    {
        if (!GodotObject.IsInstanceValid(parent) || ChildScene?.Instantiate() is not Node3D child)
            return;

        if (child is CardContainer container)
        {
            // Children share this container's card spawner. A seeded parent seeds each child before it enters the
            // tree; a test-set Generator still passes down unchanged.
            container.Generator = Generator;
            container.Spawning = Spawning;
            if (ContentSeed is { } seed)
                container.ContentSeed = ChildSeed(seed, index);
        }

        parent.AddChild(child);
        child.GlobalPosition = position;
    }
}
