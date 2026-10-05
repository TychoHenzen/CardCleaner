using System;
using System.Collections.Generic;
using CardCleaner.Scripts.Core.DependencyInjection;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     A box, pack or booster the player opens by looking at it and interacting. Opening a box spawns
///     8 packs, a pack spawns 8 boosters and a booster spawns 8 cards whose signatures come from
///     <see cref="CardPackGenerator" />. The contents appear one per frame (like the debug card spawner)
///     on a grid above the container, so even a full 512-card box never stalls a frame or stacks bodies
///     on top of each other.
/// </summary>
public partial class CardContainer : RigidBody3D, IInteractable
{
    public const int Columns = 4;
    public const float SpacingX = 0.8f;
    public const float SpacingZ = 1.0f;
    public const float LiftHeight = 0.4f;

    private const float DefaultInteractionRange = 4.5f;
    private const uint InteractableLayerBit = 4; // Layer 3, in the InteractionSystem mask

    private readonly Queue<Action> _pendingSpawns = new();

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

    /// <summary>Decides the cards in a booster. Resolved at startup; assignable directly in tests.</summary>
    public CardPackGenerator? Generator { get; set; }

    /// <summary>Spawns the cards. Resolved at startup; assignable directly in tests.</summary>
    public ICardSpawningService? Spawning { get; set; }

    public bool IsOpened { get; private set; }

    /// <summary>Items opened out of this container so far.</summary>
    public int SpawnedCount { get; private set; }

    /// <summary>Items still waiting to be spawned over the next frames.</summary>
    public int PendingCount => _pendingSpawns.Count;

    public bool CanInteract => !IsOpened && HasContents;

    public Node3D InteractionBody => this;

    private bool HasContents =>
        Kind == CardContainerKind.Booster ? Spawning != null && Generator != null : ChildScene != null;

    public override void _Ready()
    {
        CollisionLayer |= InteractableLayerBit;
        ClearHighlight();
        SetProcess(false);

        ServiceLocator.Get<RandomNumberGenerator>(rng => Generator ??= new CardPackGenerator(rng));
        ServiceLocator.Get<ICardSpawningService>(service => Spawning ??= service);
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

    public void Interact() => Open();

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
    ///     Offset of the n-th item opened out of a container of <paramref name="kind" />: a grid of
    ///     <see cref="Columns" /> columns. A booster lays its cards on the base grid; each level above
    ///     multiplies the grid by the footprint of the level below, so the items of sibling containers
    ///     (cards of two boosters, boosters of two packs) never land on the same spot.
    /// </summary>
    public static Vector3 SpawnOffset(int index, CardContainerKind kind)
    {
        var rows = (CardContainerLayout.ItemsPerContainer + Columns - 1) / Columns;
        var levels = CardContainerLayout.LevelsBelow(kind);
        var column = index % Columns;
        var row = index / Columns;
        return new Vector3(
            (column - (Columns - 1) / 2f) * SpacingX * MathF.Pow(Columns, levels),
            LiftHeight,
            (row - (rows - 1) / 2f) * SpacingZ * MathF.Pow(rows, levels));
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
        var signatures = Generator!.OpenBooster();
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
            var position = origin + SpawnOffset(i, Kind);
            _pendingSpawns.Enqueue(() => SpawnContainer(parent, position));
        }
    }

    private void SpawnCard(Node3D parent, CardSignature signature, Vector3 position)
    {
        if (!GodotObject.IsInstanceValid(parent))
            return;

        var transform = new Transform3D(Basis.Identity, position);
        Spawning?.SpawnCard(signature, transform, parent);
    }

    private void SpawnContainer(Node3D parent, Vector3 position)
    {
        if (!GodotObject.IsInstanceValid(parent) || ChildScene?.Instantiate() is not Node3D child)
            return;

        if (child is CardContainer container)
        {
            // Children share this container's random source and card spawner, so one seed drives the whole box.
            container.Generator = Generator;
            container.Spawning = Spawning;
        }

        parent.AddChild(child);
        child.GlobalPosition = position;
    }
}
