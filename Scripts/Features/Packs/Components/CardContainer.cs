using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Core.Randomness;
using CardCleaner.Scripts.Features.Card.Components;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     A cardboard box, booster box (a pack) or booster pack. A cardboard box is storage: it holds whatever its
///     <see cref="CartonStorage" /> keeps, is never opened whole, and stays a box when it is empty. Its flaps
///     (<see cref="SetLidOpen" />) open in the hand to take its items out one at a time onto a shelf
///     (<see cref="TakeOneInto" />). A held booster box empties into the hand as its eight booster packs
///     (<see cref="EmptyIntoHand" />), and a held booster opens straight into the hand of cards
///     (<see cref="OpenInHand" />, signatures from <see cref="CardPackGenerator" />). Nothing opens on the floor: a
///     container is opened only while the player holds it.
/// </summary>
public partial class CardContainer : RigidBody3D, IInteractable, IDependent
{
    // Seconds a cardboard box's flaps take to swing open or shut in the hand.
    private const float LidSeconds = 0.4f;

    /// <summary>
    ///     Raised when the player interacts with a container that can be opened or carried. The holder that
    ///     listens for it decides whether the container is picked up; interacting never opens it directly.
    /// </summary>
    [Signal]
    public delegate void PickUpRequestedEventHandler(CardContainer container);

    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    private BoosterReveal? _opening;
    private IServiceResolver? _services;
    private IRandom? _random;
    private List<OpeningPart>? _lidParts;
    private CartonStorage? _storage;

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

    /// <summary>True while a holder carries this container (its parent is the holder's camera).</summary>
    public bool IsHeld => GetParent() is Camera3D;

    /// <summary>True while this booster opens in the player's hand (<see cref="OpenInHand" />).</summary>
    public bool IsOpeningInHand => _opening is { IsOver: false };

    /// <summary>True for a cardboard box, whose flaps open in the hand; a booster box empties whole instead.</summary>
    public bool HasLid => Kind == CardContainerKind.Box;

    /// <summary>True while the lid or flaps are open (<see cref="SetLidOpen" />).</summary>
    public bool IsLidOpen { get; private set; }

    /// <summary>What a cardboard box holds; null for a pack or booster.</summary>
    public CartonStorage? Storage =>
        Kind == CardContainerKind.Box ? _storage ??= GetNodeOrNull<CartonStorage>("Storage") : null;

    /// <summary>
    ///     Items inside: what a cardboard box holds, or the 8 a booster box or booster pack is packed with until it is
    ///     opened.
    /// </summary>
    public int Remaining => Storage?.Count ?? CardContainerLayout.ItemsPerContainer;

    // A cardboard box can always be carried, even empty, so the player can take it to the trash can.
    public bool CanInteract => !IsOpened && !IsHeld && (Kind == CardContainerKind.Box || HasContents);

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
        // The card highlighter finds every container through this group, including the booster packs a booster box
        // empties into the hand later.
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
    ///     source (the only time a container draws a seed, so opening unseeded containers keeps its draw order). Null when
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
    ///     Opens or closes the flaps of a cardboard box. They swing over a fraction of a second, or move at once when
    ///     <paramref name="instantly" /> (a load). Nothing comes out: items leave an open box one at a time through
    ///     <see cref="TakeOneInto" />. False for a booster box, a booster or a container already opened.
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

        if (open)
            Storage?.ShowContents();

        SetProcess(LidMoving);
        return true;
    }

    private float LidTarget => IsLidOpen ? 1f : 0f;

    private bool LidMoving => _opening == null && !Mathf.IsEqualApprox(_lidProgress, LidTarget);

    // The parts keep their rest pose from their own _Ready, so they are posed only once this container is ready. A
    // booster's tear strip is left to its opening in the hand.
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
    ///     Locks the last item this open cardboard box holds that <paramref name="slot" /> takes into it; false, with
    ///     nothing taken, when the flaps are shut or nothing fits. An emptied box stays a box.
    /// </summary>
    public bool TakeOneInto(DeckSlot slot) =>
        IsLidOpen && !IsOpened && Storage is { } storage && storage.TakeInto(slot);

    // A booster taken out of the tree while it opens in the hand (a load clears the scene's containers) hands the
    // cards already out of it to the hand.
    public override void _ExitTree()
    {
        _opening?.Abandon();
        SetProcess(false);
    }

    /// <summary>
    ///     Ends a booster's opening in the hand at once, as if its time had run out: every card is in the hand and the
    ///     wrapper is freed. A save calls it first, so it never has to store a half-opened booster. Does nothing when
    ///     the container is not opening.
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
    ///     Empties this held booster box into the hand: its eight booster packs are built under the hand anchor, the
    ///     n-th seeded with <see cref="ContainerSeeds.ChildSeed" /> of this box's seed and n, and the empty box is used
    ///     up: it counts as opened, leaves the tree and is freed. Returns the booster packs in order, for the holder to
    ///     take; none unless this is a held, unopened booster box with a booster scene.
    /// </summary>
    public IReadOnlyList<CardContainer> EmptyIntoHand()
    {
        if (Kind != CardContainerKind.Pack || IsOpened || !IsHeld || !HasContents || GetParent() is not Node3D anchor)
            return [];

        IsOpened = true;
        ClearHighlight();
        var boosters = new List<CardContainer>(CardContainerLayout.ItemsPerContainer);
        for (var i = 0; i < CardContainerLayout.ItemsPerContainer; i++)
        {
            if (SpawnContainer(anchor, GlobalPosition, i) is CardContainer booster)
                boosters.Add(booster);
        }

        // ASSUMPTION: an emptied booster box is thrown away at once (Risk Low). Unlike a cardboard box it stores
        // nothing, so the player has no use for it, and it leaves the tree now so the hand never shows it.
        anchor.RemoveChild(this);
        QueueFree();
        return boosters;
    }

    /// <summary>
    ///     Opens this held booster in front of the player (<see cref="BoosterReveal" />): its strip tears off, its
    ///     cards spread into a fan, and the fan slides into <paramref name="hand" />. The cards come from this
    ///     booster's seed (<see cref="CardPackGenerator.OpenBooster" />). The booster stays under the hand anchor the
    ///     whole time and is freed when the cards are in the hand. False unless this is a held, unopened booster that
    ///     has its contents.
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

    // A test-set Generator, else one drawing from the saved content seed, else from the shared random source.
    private CardPackGenerator CardGenerator() =>
        Generator ?? new CardPackGenerator(ContentSeed is { } seed ? RandomSources.Create(seed) : _random!);

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
