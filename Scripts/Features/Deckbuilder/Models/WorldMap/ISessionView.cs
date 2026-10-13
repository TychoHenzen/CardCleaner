using System.Collections.Generic;
using CardCleaner.Scripts.Core.Interfaces;
using CardCleaner.Scripts.Features.Card.Models;
using CardCleaner.Scripts.Features.Deckbuilder.Services;
using Godot;

namespace CardCleaner.Scripts.Features.Deckbuilder.Models;

/// <summary>
/// A map view that a <see cref="SessionViewLink"/> connects to the game session. The link calls the event members
/// only while the view owns the session, with cell ids and world positions; the view converts them to its own
/// coordinates.
/// </summary>
internal interface ISessionView
{
    /// <summary>
    /// Whether the view is still live, so a session service that resolves late may still connect it; a view that left
    /// the tree or was freed must refuse.
    /// </summary>
    bool CanConnect { get; }

    /// <summary>
    /// Called right before the link starts a session for the view: clear what the last session left, and return the
    /// line-of-sight checker for the new session, or null for the session's own.
    /// </summary>
    IVisibilityChecker? PrepareForSession();

    /// <summary>Called when the view stops owning the session.</summary>
    void OnSessionReleased();

    /// <summary>Every state change of the session; after the fall back to waiting, the link releases it.</summary>
    void OnStateChanged(SessionState state);

    void OnMapReady(IGeneratedMap map);

    /// <summary>The player moved; the view reports the move shown through the link once it has shown it.</summary>
    void OnPlayerMoved(Vector2 worldPosition);

    void OnVisibilityUpdated(IReadOnlySet<int> seenCells, IReadOnlySet<int> visibleCells);

    void OnPathUpdated(IReadOnlyList<int> pathCells, int? targetCell);

    void OnEnemyDefeated(int cellId);

    void OnLootGenerated(List<CardSignature> loot);
}
