using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Shop;

/// <summary>
///     Spawns light card bodies (<see cref="ShopTestCards" />) with the signature and look seed asked for, under the
///     parent and at the pose given, as the real spawner does. It keeps the cards in spawn order.
/// </summary>
public sealed class ShopTestCardSpawner : ICardSpawningService
{
    public List<CardController> Spawned { get; } = [];

    public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent) =>
        SpawnCard(signature, null, spawnTransform, parent);

    public Node3D? SpawnCard(CardSignature signature, int? lookSeed, Transform3D spawnTransform, Node3D parent)
    {
        var card = ShopTestCards.Create(signature);
        card.LookSeed = lookSeed;
        parent.AddChild(card);
        card.GlobalTransform = spawnTransform;
        Spawned.Add(card);
        return card;
    }

    public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

    public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
}
