using CardCleaner.Scripts.Features.Card.Controllers;
using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Packs;

/// <summary>
///     Records every card a container asks for. By default it builds no card nodes; with
///     <paramref name="buildBodies" /> it builds a bare card for each one (a <see cref="CardController" /> with the
///     "CardCollision" shape a hand expects), so a test can follow the cards into the hand.
/// </summary>
public sealed class RecordingCardSpawner(bool buildBodies = false) : ICardSpawningService
{
    public List<(CardSignature Signature, Transform3D Transform)> Spawned { get; } = [];

    /// <summary>The bodies built so far, in spawn order (empty unless bodies are built).</summary>
    public List<RigidBody3D> Bodies { get; } = [];

    public int SpecialCount => Spawned.Count(s => s.Signature.HasMagicalPotential());

    public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
    {
        Spawned.Add((signature, spawnTransform));
        if (!buildBodies)
            return null;

        var card = new CardController { Name = $"Card{Bodies.Count}" };
        card.AddChild(new CollisionShape3D { Name = "CardCollision", Shape = new BoxShape3D() });
        parent.AddChild(card);
        card.GlobalTransform = spawnTransform;
        Bodies.Add(card);
        return card;
    }

    public Node3D? SpawnCard(
        CardSignature signature, int? lookSeed, Transform3D spawnTransform, Node3D parent) =>
        SpawnCard(signature, spawnTransform, parent);

    public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

    public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
}
