using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Controllers;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.SaveSlots;

/// <summary>
///     Records each respawn and, unless told otherwise, adds the card under its parent as the real spawner does.
/// </summary>
internal sealed class SpawnRecorder(bool addToTree = true) : ICardSpawningService
{
    // Cards kept out of the tree have no parent to free them, so the recorder frees them in FreeDetached.
    private readonly List<CardController> _detached = [];

    public List<Spawn> Spawned { get; } = [];

    public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent) =>
        SpawnCard(signature, null, spawnTransform, parent);

    public Node3D? SpawnCard(CardSignature signature, int? lookSeed, Transform3D spawnTransform, Node3D parent)
    {
        var card = new CardController { Signature = signature };
        if (addToTree)
            parent.AddChild(card);
        else
            _detached.Add(card);
        Spawned.Add(new Spawn(signature, spawnTransform, card, lookSeed));
        return card;
    }

    /// <summary>Frees every card this recorder kept out of the tree that nothing else has parented or freed.</summary>
    public void FreeDetached()
    {
        foreach (var card in _detached)
        {
            if (GodotObject.IsInstanceValid(card) && card.GetParent() == null)
                card.Free();
        }

        _detached.Clear();
    }

    public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

    public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;

    internal sealed record Spawn(
        CardSignature Signature,
        Transform3D Transform,
        CardController Card,
        int? LookSeed = null);
}
