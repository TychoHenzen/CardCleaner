using Godot;
using CardCleaner.Scripts.Features.Card.Models;

namespace CardCleaner.Scripts.Core.Interfaces;

/// <summary>
/// Service for spawning card instances with proper generation and setup.
/// </summary>
public interface ICardSpawningService
{
    /// <summary>
    /// Spawns a single card with the given signature at the specified transform.
    /// </summary>
    /// <param name="signature">The card signature to generate</param>
    /// <param name="spawnTransform">World transform for the spawned card</param>
    /// <param name="parent">Parent node to add the card to</param>
    /// <returns>The spawned card instance</returns>
    Node3D SpawnCard(CardSignature signature, Transform3D spawnTransform, Node3D parent);

    /// <summary>
    /// Spawns a card with a random signature.
    /// </summary>
    /// <param name="spawnTransform">World transform for the spawned card</param>
    /// <param name="parent">Parent node to add the card to</param>
    /// <returns>The spawned card instance</returns>
    Node3D SpawnRandomCard(Transform3D spawnTransform, Node3D parent);

    /// <summary>
    /// Gets a random offset within the specified range.
    /// </summary>
    /// <param name="offsetRange">Maximum offset in each direction</param>
    /// <returns>Random offset vector</returns>
    Vector3 GetRandomOffset(Vector3 offsetRange);
}