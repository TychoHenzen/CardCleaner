using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using CardCleaner.Scripts.Features.Packs.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Turns a card, a sealed booster pack or booster box, or a cardboard box into its record
///     (<see cref="StoredItem" />), and a record back into a body in the world with the same signature, look, seed and
///     price, or the same contents and flaps.
/// </summary>
internal static class StoredItemBodies
{
    /// <summary>
    ///     The shape <paramref name="body" /> takes in a box, or null when a box does not keep it: anything but a card,
    ///     or a booster pack or booster box that is sealed, shut, whole and built from a scene file.
    /// </summary>
    public static ItemShape? ShapeOf(RigidBody3D body)
    {
        return body switch
        {
            CardController => ItemShape.Card,
            CardContainer { Kind: CardContainerKind.Booster } booster when IsWhole(booster) => ItemShape.Booster,
            CardContainer { Kind: CardContainerKind.Pack } pack when IsWhole(pack) => ItemShape.Pack,
            _ => null
        };
    }

    /// <summary>
    ///     Whether the transfer rule moves <paramref name="body" /> as a record: what <see cref="ShapeOf" /> accepts,
    ///     and a cardboard box, which no other box keeps.
    /// </summary>
    public static bool IsRecorded(RigidBody3D body) =>
        ShapeOf(body) != null
        || body is CardContainer { Kind: CardContainerKind.Box, IsOpened: false } box
        && !string.IsNullOrEmpty(box.SceneFilePath);

    /// <summary>
    ///     The record for a body <see cref="IsRecorded" /> accepts. A container without a seed draws one now, so it
    ///     opens into the same items when it comes out again.
    /// </summary>
    public static StoredItem RecordOf(RigidBody3D body)
    {
        return body switch
        {
            CardController card => new StoredCard(card.HasSignature ? card.Signature : null, card.LookSeed,
                card.ShopPrice),
            CardContainer { Kind: CardContainerKind.Box } box => BoxRecordOf(box),
            CardContainer container => new StoredContainer(container.SceneFilePath, container.Kind,
                container.SeedForSave(), container.ShopPrice),
            _ => throw new System.ArgumentException($"{body.Name} is not stock a box keeps", nameof(body))
        };
    }

    /// <summary>
    ///     The record <paramref name="body" /> stands for now, without drawing a seed: a look at an item that is not
    ///     moved yet (a prompt, a room check) must not change the shared random source's draw order.
    /// </summary>
    public static StoredItem CurrentRecordOf(RigidBody3D body) => body switch
    {
        CardContainer { Kind: CardContainerKind.Box } box => BoxRecordOf(box),
        CardContainer container =>
            new StoredContainer(container.SceneFilePath, container.Kind, container.ContentSeed, container.ShopPrice),
        _ => RecordOf(body)
    };

    // A box keeps its own seed as it is (a seed is drawn only when a save asks), and what it holds as it stores it.
    private static StoredBox BoxRecordOf(CardContainer box) =>
        new(box.SceneFilePath, box.ContentSeed, box.Storage?.SavedItems, box.IsLidOpen);

    /// <summary>
    ///     Builds <paramref name="item" /> under <paramref name="parent" /> at <paramref name="at" />, sharing the
    ///     card spawner and test generator of <paramref name="box" />. Null when it could not be built.
    /// </summary>
    public static RigidBody3D? Build(StoredItem item, Node3D parent, Transform3D at, CardContainer box) =>
        Build(item, parent, at, box.Spawning, box.Generator);

    /// <summary>
    ///     Builds <paramref name="item" /> under <paramref name="parent" /> at <paramref name="at" />. A card needs
    ///     <paramref name="spawning" />; a container left without one picks up the registered spawner when it is
    ///     ready. Null when it could not be built.
    /// </summary>
    public static RigidBody3D? Build(StoredItem item, Node3D parent, Transform3D at, ICardSpawningService? spawning,
        CardPackGenerator? generator = null)
    {
        return item switch
        {
            StoredCard card => BuildCard(card, parent, at, spawning),
            StoredBox box => BuildBox(box, parent, at, spawning, generator),
            StoredContainer container => BuildContainer(container, parent, at, spawning, generator),
            _ => null
        };
    }

    private static bool IsWhole(CardContainer container) =>
        !container.IsOpened && !container.IsLidOpen && !string.IsNullOrEmpty(container.SceneFilePath);

    /// <summary>
    ///     Spawns the card <paramref name="stored" /> describes under <paramref name="parent" /> at
    ///     <paramref name="at" />, with its signature (or none), look and price. Null when it could not be spawned.
    /// </summary>
    public static CardController? BuildCard(StoredCard stored, Node3D parent, Transform3D at,
        ICardSpawningService? spawning)
    {
        if (spawning?.SpawnCard(stored.Signature ?? new CardSignature(), stored.LookSeed, at, parent)
            is not CardController card)
            return null;

        if (stored.Signature == null)
            card.ClearSignature();
        card.ShopPrice = stored.Price;
        return card;
    }

    private static CardContainer? BuildBox(StoredBox stored, Node3D parent, Transform3D at,
        ICardSpawningService? spawning, CardPackGenerator? generator)
    {
        if (Instantiate(stored.ScenePath) is not { } box)
            return null;

        // What the box holds is set before it enters the tree, so it is shown as it was; its flaps swing open after.
        box.ContentSeed = stored.Seed;
        box.Spawning = spawning;
        box.Generator = generator;
        box.Storage?.Restore(stored.Contents);
        parent.AddChild(box);
        box.GlobalTransform = at;
        if (stored.LidOpen)
            box.SetLidOpen(true, instantly: true);
        return box;
    }

    private static CardContainer? BuildContainer(StoredContainer stored, Node3D parent, Transform3D at,
        ICardSpawningService? spawning, CardPackGenerator? generator)
    {
        if (Instantiate(stored.ScenePath) is not { } container)
            return null;

        // Set before AddChild, so the container never draws a seed of its own.
        container.ContentSeed = stored.Seed;
        container.ShopPrice = stored.Price;
        container.Spawning = spawning;
        container.Generator = generator;
        parent.AddChild(container);
        container.GlobalTransform = at;
        return container;
    }

    private static CardContainer? Instantiate(string scenePath)
    {
        var node = ResourceLoader.Load<PackedScene>(scenePath)?.Instantiate();
        if (node is CardContainer container)
            return container;

        // A scene whose root is no container is freed here, since nothing else holds it.
        node?.Free();
        ILog.Error($"{scenePath} is no card container, so its stored item could not be built");
        return null;
    }
}
