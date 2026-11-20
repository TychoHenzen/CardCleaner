using CardCleaner.Scripts.Features.Card.Models;
using Godot;

namespace CardCleaner.Scripts.Features.Card.Services.SaveGame;

/// <summary>
/// One persisted card read back from save data, ready to be spawned again. <see cref="Price" /> is the shop price the
/// player set, or null when the card asks its market price. A card the hand holds is saved with the hand
/// (<see cref="SavedHand" />), never as one of these.
/// </summary>
internal sealed record SavedCardRecord(
    CardSignature? Signature,
    Vector3 Position,
    Vector3 Rotation,
    string ParentPath,
    int? LookSeed = null,
    int? Price = null);
