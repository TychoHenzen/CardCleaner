using System.Collections.Generic;
using System.Linq;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Tests.Features.Packs;

/// <summary>Records every card a container asks for instead of building real card nodes.</summary>
public sealed class RecordingCardSpawner : ICardSpawningService
{
    public List<(CardSignature Signature, Transform3D Transform)> Spawned { get; } = [];

    public int SpecialCount => Spawned.Count(s => s.Signature.HasMagicalPotential());

    public Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent)
    {
        Spawned.Add((signature, spawnTransform));
        return null;
    }

    public Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent) => null;

    public Vector3 GetRandomOffset(Vector3 offsetRange) => Vector3.Zero;
}
