using Godot;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Service for spawning card instances with proper generation and setup.
/// </summary>
public interface ICardSpawningService
{
    /// <summary>Spawns a card with the given signature at the specified transform.</summary>
    Node3D? SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent);

    /// <summary>Spawns a card with a random signature.</summary>
    Node3D? SpawnRandomCard(Transform3D spawnTransform, Node3D parent);

    /// <summary>Gets a random offset within the specified range.</summary>
    Vector3 GetRandomOffset(Vector3 offsetRange);
}
