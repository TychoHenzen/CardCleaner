using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Packs.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Packs.Components;

/// <summary>
///     Turns a card or a sealed booster pack or booster box into the record a cardboard box keeps
///     (<see cref="StoredItem" />), and a record back into a body in the world with the same signature, look, seed and
///     price.
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
    ///     The record for a body <see cref="ShapeOf" /> accepts. A container without a seed draws one now, so it opens
    ///     into the same items when it comes out again.
    /// </summary>
    public static StoredItem RecordOf(RigidBody3D body)
    {
        return body switch
        {
            CardController card => new StoredCard(card.HasSignature ? card.Signature : null, card.LookSeed,
                card.ShopPrice),
            CardContainer container => new StoredContainer(container.SceneFilePath, container.Kind,
                container.SeedForSave(), container.ShopPrice),
            _ => throw new System.ArgumentException($"{body.Name} is not stock a box keeps", nameof(body))
        };
    }

    /// <summary>
    ///     Builds <paramref name="item" /> under <paramref name="parent" /> at <paramref name="at" />, sharing the
    ///     card spawner and test generator of <paramref name="box" />. Null when it could not be built.
    /// </summary>
    public static RigidBody3D? Build(StoredItem item, Node3D parent, Transform3D at, CardContainer box)
    {
        return item switch
        {
            StoredCard card => BuildCard(card, parent, at, box.Spawning),
            StoredContainer container => BuildContainer(container, parent, at, box),
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

    private static CardContainer? BuildContainer(StoredContainer stored, Node3D parent, Transform3D at,
        CardContainer box)
    {
        var node = ResourceLoader.Load<PackedScene>(stored.ScenePath)?.Instantiate();
        if (node is not CardContainer container)
        {
            // A scene whose root is no container is freed here, since nothing else holds it.
            node?.Free();
            ILog.Error($"{stored.ScenePath} is no card container, so its stored item could not be built");
            return null;
        }

        // Set before AddChild, so the container never draws a seed of its own.
        container.ContentSeed = stored.Seed;
        container.ShopPrice = stored.Price;
        container.Spawning = box.Spawning;
        container.Generator = box.Generator;
        parent.AddChild(container);
        container.GlobalTransform = at;
        return container;
    }
}
